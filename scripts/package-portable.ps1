[CmdletBinding()]
param(
    [string]$Version = '0.2.1',
    [string]$MsysRoot = 'C:\msys64',
    [switch]$SkipPublish
)

$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path -Parent $PSScriptRoot
$artifactsRoot = Join-Path $repoRoot 'artifacts'
$publishDirectory = Join-Path $artifactsRoot 'LocalPlay'
$packageRoot = Join-Path $artifactsRoot 'package'
$stageDirectory = Join-Path $packageRoot 'LocalPlay'
$engineDirectory = Join-Path $stageDirectory 'engine'
$pluginDirectory = Join-Path $engineDirectory 'lib\gstreamer-1.0'
$scannerDirectory = Join-Path $engineDirectory 'libexec\gstreamer-1.0'
$licensesDirectory = Join-Path $stageDirectory 'licenses'
$sourceDirectory = Join-Path $stageDirectory 'source'
$msysRoot = [IO.Path]::GetFullPath($MsysRoot)
$ucrtRoot = Join-Path $msysRoot 'ucrt64'
$ucrtBin = Join-Path $ucrtRoot 'bin'
$ucrtPlugins = Join-Path $ucrtRoot 'lib\gstreamer-1.0'
$uxplaySource = Join-Path $repoRoot '.deps\UxPlay'
$uxplayExecutable = Join-Path $ucrtBin 'uxplay.exe'
$objdumpExecutable = Join-Path $ucrtBin 'objdump.exe'
$pluginScanner = Join-Path $ucrtRoot 'libexec\gstreamer-1.0\gst-plugin-scanner.exe'
$gstInspect = Join-Path $ucrtBin 'gst-inspect-1.0.exe'

if ($Version.StartsWith('v', [StringComparison]::OrdinalIgnoreCase)) {
    $Version = $Version.Substring(1)
}

$workspaceDotnet = Join-Path $repoRoot '.tools\dotnet\dotnet.exe'
$dotnetExe = if (Test-Path $workspaceDotnet) {
    $workspaceDotnet
} else {
    (Get-Command dotnet -ErrorAction SilentlyContinue).Source
}

if (-not $dotnetExe) {
    throw 'The .NET 8 SDK is missing.'
}

$resolvedArtifactsRoot = [IO.Path]::GetFullPath($artifactsRoot)
$resolvedPublishDirectory = [IO.Path]::GetFullPath($publishDirectory)
$artifactsPrefix = $resolvedArtifactsRoot + [IO.Path]::DirectorySeparatorChar
if (-not $resolvedPublishDirectory.StartsWith(
    $artifactsPrefix,
    [StringComparison]::OrdinalIgnoreCase)) {
    throw 'Refusing to clean a publish directory outside artifacts.'
}

foreach ($requiredPath in @(
    $uxplayExecutable,
    $objdumpExecutable,
    $pluginScanner,
    $gstInspect
)) {
    if (-not (Test-Path $requiredPath)) {
        throw "Required runtime file is missing: $requiredPath. Run scripts\bootstrap.ps1 first."
    }
}

if (-not $SkipPublish) {
    if (Test-Path $publishDirectory) {
        Remove-Item -LiteralPath $publishDirectory -Recurse -Force
    }

    Write-Host 'Publishing the self-contained Windows app...'
    $project = Join-Path $repoRoot 'src\LocalPlay.App\LocalPlay.App.csproj'
    & $dotnetExe publish $project -c Release -r win-x64 --self-contained true `
        -p:Version=$Version -o $publishDirectory
    if ($LASTEXITCODE -ne 0) {
        throw "LocalPlay publish failed with exit code $LASTEXITCODE."
    }
}

if (-not (Test-Path (Join-Path $publishDirectory 'LocalPlay.exe'))) {
    throw 'The published LocalPlay app is missing.'
}

$resolvedPackageRoot = [IO.Path]::GetFullPath($packageRoot)
if (-not $resolvedPackageRoot.StartsWith(
    $artifactsPrefix,
    [StringComparison]::OrdinalIgnoreCase)) {
    throw 'Refusing to clean a package directory outside artifacts.'
}

if (Test-Path $packageRoot) {
    Remove-Item -LiteralPath $packageRoot -Recurse -Force
}

New-Item -ItemType Directory -Force -Path `
    $stageDirectory,
    $engineDirectory,
    $pluginDirectory,
    $scannerDirectory,
    $licensesDirectory,
    $sourceDirectory | Out-Null

Copy-Item -Path (Join-Path $publishDirectory '*') -Destination $stageDirectory -Recurse -Force
Copy-Item -LiteralPath $uxplayExecutable -Destination $engineDirectory
Copy-Item -LiteralPath $gstInspect -Destination $engineDirectory
Copy-Item -LiteralPath $pluginScanner -Destination $scannerDirectory

$pluginNames = @(
    'libgstapp.dll',
    'libgstcoreelements.dll',
    'libgstplayback.dll',
    'libgstvideoparsersbad.dll',
    'libgstvideoconvertscale.dll',
    'libgstd3d11.dll',
    'libgstwasapi.dll',
    'libgstwasapi2.dll',
    'libgstaudioconvert.dll',
    'libgstaudioresample.dll',
    'libgstvolume.dll',
    'libgstlevel.dll',
    'libgstlibav.dll',
    'libgsttypefindfunctions.dll',
    'libgstautodetect.dll',
    'libgstjpeg.dll',
    'libgstimagefreeze.dll',
    'libgstpango.dll',
    'libgstvideofilter.dll'
)

$seedFiles = [Collections.Generic.List[string]]::new()
$seedFiles.Add($uxplayExecutable)
$seedFiles.Add($gstInspect)
$seedFiles.Add($pluginScanner)

foreach ($pluginName in $pluginNames) {
    $source = Join-Path $ucrtPlugins $pluginName
    if (-not (Test-Path $source)) {
        throw "Required GStreamer plugin is missing: $pluginName"
    }

    Copy-Item -LiteralPath $source -Destination $pluginDirectory
    $seedFiles.Add($source)
}

$runtimeByName = @{}
Get-ChildItem -LiteralPath $ucrtBin -Filter '*.dll' -File | ForEach-Object {
    $runtimeByName[$_.Name.ToLowerInvariant()] = $_.FullName
}

$queue = [Collections.Generic.Queue[string]]::new()
$seedFiles | ForEach-Object { $queue.Enqueue($_) }
$processed = [Collections.Generic.HashSet[string]]::new(
    [StringComparer]::OrdinalIgnoreCase)
$packagedSources = [Collections.Generic.HashSet[string]]::new(
    [StringComparer]::OrdinalIgnoreCase)
$seedFiles | ForEach-Object { [void]$packagedSources.Add($_) }

while ($queue.Count -gt 0) {
    $binary = $queue.Dequeue()
    if (-not $processed.Add($binary)) {
        continue
    }

    $imports = & $objdumpExecutable -p $binary 2>$null |
        Select-String 'DLL Name:\s*(.+)$' |
        ForEach-Object { $_.Matches[0].Groups[1].Value.Trim() }

    foreach ($import in $imports) {
        $key = $import.ToLowerInvariant()
        if (-not $runtimeByName.ContainsKey($key)) {
            continue
        }

        $dependency = $runtimeByName[$key]
        if ($packagedSources.Add($dependency)) {
            Copy-Item -LiteralPath $dependency -Destination $engineDirectory
            $queue.Enqueue($dependency)
        }
    }
}

Copy-Item -LiteralPath (Join-Path $repoRoot 'LICENSE') -Destination $stageDirectory
Copy-Item -LiteralPath (Join-Path $repoRoot 'THIRD_PARTY_NOTICES.md') -Destination $stageDirectory
Copy-Item -LiteralPath (Join-Path $repoRoot 'patches\uxplay-windows-mdns-interface.patch') `
    -Destination $sourceDirectory
Copy-Item -LiteralPath (Join-Path $repoRoot 'scripts\bootstrap.ps1') `
    -Destination $sourceDirectory

$uxplayLicense = Join-Path $ucrtRoot 'share\doc\uxplay\LICENSE'
if (-not (Test-Path $uxplayLicense)) {
    $uxplayLicense = Join-Path $uxplaySource 'LICENSE'
}
Copy-Item -LiteralPath $uxplayLicense `
    -Destination (Join-Path $licensesDirectory 'UxPlay-GPL-3.0.txt')
Copy-Item -LiteralPath $uxplayLicense `
    -Destination (Join-Path $licensesDirectory 'GPL-3.0.txt')

$commonLgpl = Join-Path $ucrtRoot 'share\licenses\glib2\COPYING'
if (Test-Path $commonLgpl) {
    Copy-Item -LiteralPath $commonLgpl `
        -Destination (Join-Path $licensesDirectory 'LGPL-2.1.txt')
}

$gitExe = (Get-Command git -ErrorAction SilentlyContinue).Source
if ($gitExe -and (Test-Path (Join-Path $uxplaySource '.git'))) {
    $uxplayArchive = Join-Path $sourceDirectory 'UxPlay-upstream-source.zip'
    & $gitExe -C $uxplaySource archive --format=zip `
        --output=$uxplayArchive acfb5494fb2b52ca358e62ef59d6ee0ab20dec49
    if ($LASTEXITCODE -ne 0) {
        throw 'The UxPlay source archive could not be created.'
    }
}

$pacmanExecutable = Join-Path $msysRoot 'usr\bin\pacman.exe'
$packageNames = [Collections.Generic.HashSet[string]]::new(
    [StringComparer]::OrdinalIgnoreCase)
$packageVersions = @{}
if (Test-Path $pacmanExecutable) {
    $ownershipCandidates = @(
        $packagedSources |
            Where-Object {
                -not [IO.Path]::GetFullPath($_).Equals(
                    [IO.Path]::GetFullPath($uxplayExecutable),
                    [StringComparison]::OrdinalIgnoreCase)
            } |
            ForEach-Object { $_.Replace('\', '/') }
    )
    $previousErrorPreference = $ErrorActionPreference
    $ErrorActionPreference = 'Continue'
    $ownershipLines = & $pacmanExecutable -Qo $ownershipCandidates 2>$null
    $ErrorActionPreference = $previousErrorPreference
    foreach ($line in $ownershipLines) {
        if ($line -match ' is owned by (mingw-w64-ucrt-x86_64-(\S+))\s+(\S+)$') {
            [void]$packageNames.Add($Matches[2])
            $packageVersions[$Matches[2]] = $Matches[3]
        }
    }

    foreach ($packageName in $packageNames) {
        $licenseSource = Join-Path $ucrtRoot "share\licenses\$packageName"
        if (Test-Path $licenseSource) {
            Copy-Item -LiteralPath $licenseSource `
                -Destination (Join-Path $licensesDirectory $packageName) `
                -Recurse -Force
        }
    }
}

$readmeText = @"
LocalPlay $Version for Windows x64

1. Extract the complete ZIP file.
2. Start LocalPlay.exe.
3. Open "Netzwerk" and run the network test.
4. Use "Firewall-Regeln einrichten" once and approve the Windows UAC prompt.
5. Start the receiver and select LocalPlay on the Apple device.

Keep the engine folder next to LocalPlay.exe. No installation, .NET runtime,
MSYS2, or cloud account is required.

Project: https://github.com/TheGalitube/LocalPlay
"@
$readmeText | Set-Content -LiteralPath (Join-Path $stageDirectory 'README.txt') -Encoding utf8

$packageManifest = @(
    "LocalPlay $Version",
    "UxPlay revision acfb5494fb2b52ca358e62ef59d6ee0ab20dec49",
    '',
    'Packaged MSYS2 runtime packages:',
    ($packageNames | Sort-Object | ForEach-Object { "- $_ $($packageVersions[$_])" })
)
$packageManifest | Set-Content `
    -LiteralPath (Join-Path $licensesDirectory 'PACKAGE-MANIFEST.txt') `
    -Encoding utf8

$zipName = "LocalPlay-$Version-win-x64.zip"
$zipPath = Join-Path $artifactsRoot $zipName
if (Test-Path $zipPath) {
    Remove-Item -LiteralPath $zipPath -Force
}

Compress-Archive -Path $stageDirectory -DestinationPath $zipPath -CompressionLevel Optimal
$hash = (Get-FileHash -LiteralPath $zipPath -Algorithm SHA256).Hash.ToLowerInvariant()
"$hash  $zipName" |
    Set-Content -LiteralPath "$zipPath.sha256" -Encoding ascii

Write-Host ''
Write-Host "Portable package: $zipPath"
Write-Host "SHA256: $hash"
