$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path -Parent $PSScriptRoot
$dotnetExe = Join-Path $repoRoot '.tools\dotnet\dotnet.exe'
$project = Join-Path $repoRoot 'src\LocalPlay.App\LocalPlay.App.csproj'

if (-not (Test-Path $dotnetExe)) {
    throw 'The workspace-local .NET SDK is missing. Run .\scripts\bootstrap.ps1 -SkipEngine first.'
}

& $dotnetExe build $project -c Release
if ($LASTEXITCODE -ne 0) {
    exit $LASTEXITCODE
}

Write-Host 'Build verification passed.'

