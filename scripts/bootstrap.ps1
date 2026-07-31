[CmdletBinding()]
param(
    [switch]$SkipEngine,
    [switch]$SkipPublish
)

$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path -Parent $PSScriptRoot
$toolsRoot = Join-Path $repoRoot '.tools'
$depsRoot = Join-Path $repoRoot '.deps'
$dotnetRoot = Join-Path $toolsRoot 'dotnet'
$dotnetExe = Join-Path $dotnetRoot 'dotnet.exe'
$uxplayRoot = Join-Path $depsRoot 'UxPlay'
$uxplayCommit = 'acfb5494fb2b52ca358e62ef59d6ee0ab20dec49'
$msysRoot = 'C:\msys64'
$bashExe = Join-Path $msysRoot 'usr\bin\bash.exe'
$gitExe = (Get-Command git -ErrorAction SilentlyContinue).Source
$wingetExe = (Get-Command winget -ErrorAction SilentlyContinue).Source

New-Item -ItemType Directory -Force -Path $toolsRoot, $depsRoot | Out-Null

if (-not (Test-Path $dotnetExe)) {
    Write-Host 'Installing a workspace-local .NET 8 SDK...'
    $installer = Join-Path $toolsRoot 'dotnet-install.ps1'
    Invoke-WebRequest 'https://dot.net/v1/dotnet-install.ps1' -OutFile $installer
    & $installer -Channel '8.0' -InstallDir $dotnetRoot -NoPath
}

if (-not $SkipEngine) {
    if (-not $wingetExe -and (-not (Test-Path $bashExe) -or -not $gitExe)) {
        throw 'winget is required for the automatic setup. Install App Installer from Microsoft Store and retry.'
    }

    if (-not $gitExe) {
        Write-Host 'Installing Git...'
        & $wingetExe install --id Git.Git --exact --silent `
            --accept-source-agreements --accept-package-agreements
        $gitCandidates = @(
            'C:\Program Files\Git\cmd\git.exe',
            (Join-Path $env:LOCALAPPDATA 'Programs\Git\cmd\git.exe')
        )
        $gitExe = $gitCandidates | Where-Object { Test-Path $_ } | Select-Object -First 1
    }

    if (-not $gitExe) {
        throw 'Git could not be found after installation.'
    }

    if (-not (Test-Path $bashExe)) {
        Write-Host 'Installing MSYS2 (Windows build environment for UxPlay)...'
        & $wingetExe install --id MSYS2.MSYS2 --exact --silent `
            --accept-source-agreements --accept-package-agreements
    }

    if (-not (Test-Path $bashExe)) {
        throw "MSYS2 was not found at $msysRoot after installation."
    }

    Write-Host 'Updating MSYS2...'
    & $bashExe -lc 'pacman -Syu --noconfirm'
    if ($LASTEXITCODE -ne 0) {
        throw "MSYS2 update failed with exit code $LASTEXITCODE."
    }

    Write-Host 'Installing UxPlay compiler and media dependencies...'
    & $bashExe -lc @'
pacman -S --noconfirm --needed \
  mingw-w64-ucrt-x86_64-cmake \
  mingw-w64-ucrt-x86_64-gcc \
  mingw-w64-ucrt-x86_64-ninja \
  mingw-w64-ucrt-x86_64-libplist \
  mingw-w64-ucrt-x86_64-gstreamer \
  mingw-w64-ucrt-x86_64-gst-plugins-base \
  mingw-w64-ucrt-x86_64-gst-plugins-good \
  mingw-w64-ucrt-x86_64-gst-plugins-bad \
  mingw-w64-ucrt-x86_64-gst-libav
'@
    if ($LASTEXITCODE -ne 0) {
        throw "MSYS2 dependency installation failed with exit code $LASTEXITCODE."
    }

    if (-not (Test-Path (Join-Path $uxplayRoot '.git'))) {
        & $gitExe clone https://github.com/FDH2/UxPlay.git $uxplayRoot
        if ($LASTEXITCODE -ne 0) {
            throw "UxPlay could not be cloned (exit code $LASTEXITCODE)."
        }
    }

    & $gitExe -C $uxplayRoot fetch --depth 1 origin $uxplayCommit
    if ($LASTEXITCODE -ne 0) {
        throw "The pinned UxPlay source could not be downloaded (exit code $LASTEXITCODE)."
    }
    & $gitExe -C $uxplayRoot checkout --detach $uxplayCommit
    if ($LASTEXITCODE -ne 0) {
        throw "The pinned UxPlay revision could not be checked out (exit code $LASTEXITCODE)."
    }

    $mdnsPatch = Join-Path $repoRoot 'patches\uxplay-windows-mdns-interface.patch'
    & $gitExe -C $uxplayRoot apply --check $mdnsPatch 2>$null
    if ($LASTEXITCODE -eq 0) {
        & $gitExe -C $uxplayRoot apply $mdnsPatch
        if ($LASTEXITCODE -ne 0) {
            throw "The LocalPlay UxPlay mDNS patch could not be applied."
        }
    } else {
        & $gitExe -C $uxplayRoot apply --reverse --check $mdnsPatch 2>$null
        if ($LASTEXITCODE -ne 0) {
            throw "The UxPlay source does not match the LocalPlay mDNS patch."
        }
    }

    Copy-Item -LiteralPath (Join-Path $repoRoot 'src\LocalPlay.App\Assets\LocalPlay.ico') `
        -Destination (Join-Path $uxplayRoot 'localplay.ico') -Force

    $uxplayUnixPath = ($uxplayRoot -replace '\\', '/')
    if ($uxplayUnixPath -match '^([A-Za-z]):/(.*)$') {
        $uxplayUnixPath = "/$($Matches[1].ToLower())/$($Matches[2])"
    }

    Write-Host 'Building UxPlay with its self-contained mDNS responder...'
    & $bashExe -lc "export PATH=/ucrt64/bin:`$PATH; cd '$uxplayUnixPath'; cmake -S . -B build -G Ninja -DUSE_MDNS=1 -DNO_MARCH_NATIVE=ON && cmake --build build && cmake --install build --prefix /ucrt64"
    if ($LASTEXITCODE -ne 0) {
        throw "UxPlay build failed with exit code $LASTEXITCODE."
    }
}

if (-not $SkipPublish) {
    Write-Host 'Publishing LocalPlay...'
    $project = Join-Path $repoRoot 'src\LocalPlay.App\LocalPlay.App.csproj'
    $output = Join-Path $repoRoot 'artifacts\LocalPlay'
    & $dotnetExe publish $project -c Release -r win-x64 --self-contained true -o $output
    if ($LASTEXITCODE -ne 0) {
        throw "LocalPlay publish failed with exit code $LASTEXITCODE."
    }
}

Write-Host ''
Write-Host 'LocalPlay is ready.'
Write-Host "Run: $(Join-Path $repoRoot 'scripts\run.ps1')"
