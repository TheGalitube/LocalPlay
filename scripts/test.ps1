$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path -Parent $PSScriptRoot
$project = Join-Path $repoRoot 'src\LocalPlay.App\LocalPlay.App.csproj'
$workspaceDotnet = Join-Path $repoRoot '.tools\dotnet\dotnet.exe'
$dotnetExe = if (Test-Path $workspaceDotnet) {
    $workspaceDotnet
} else {
    (Get-Command dotnet -ErrorAction SilentlyContinue).Source
}

if (-not $dotnetExe) {
    throw 'The .NET 8 SDK is missing. Run .\scripts\bootstrap.ps1 -SkipEngine first.'
}

& $dotnetExe restore $project
if ($LASTEXITCODE -ne 0) {
    exit $LASTEXITCODE
}

& $dotnetExe build $project -c Release --no-restore
if ($LASTEXITCODE -ne 0) {
    exit $LASTEXITCODE
}

Write-Host 'Build verification passed.'
