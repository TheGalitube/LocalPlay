$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path -Parent $PSScriptRoot
$app = Join-Path $repoRoot 'artifacts\LocalPlay\LocalPlay.exe'
$bundledEngine = Join-Path $repoRoot 'artifacts\LocalPlay\engine\uxplay.exe'
$systemEngine = 'C:\msys64\ucrt64\bin\uxplay.exe'

if (-not (Test-Path $app)) {
    Write-Host 'LocalPlay is not ready yet. Starting the one-time setup...'
    if ((Test-Path $bundledEngine) -or (Test-Path $systemEngine)) {
        & (Join-Path $PSScriptRoot 'bootstrap.ps1') -SkipEngine
    } else {
        & (Join-Path $PSScriptRoot 'bootstrap.ps1')
    }
    if ($LASTEXITCODE -ne 0) {
        exit $LASTEXITCODE
    }
} elseif (-not (Test-Path $bundledEngine) -and -not (Test-Path $systemEngine)) {
    Write-Host 'The AirPlay engine is missing. Starting the one-time setup...'
    & (Join-Path $PSScriptRoot 'bootstrap.ps1')
    if ($LASTEXITCODE -ne 0) {
        exit $LASTEXITCODE
    }
}

Start-Process -FilePath $app
