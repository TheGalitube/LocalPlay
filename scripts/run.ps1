$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path -Parent $PSScriptRoot
$app = Join-Path $repoRoot 'artifacts\LocalPlay\LocalPlay.exe'

if (-not (Test-Path $app)) {
    throw 'LocalPlay is not built. Run .\scripts\bootstrap.ps1 first.'
}

Start-Process -FilePath $app

