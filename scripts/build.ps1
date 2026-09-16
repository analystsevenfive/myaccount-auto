$ErrorActionPreference = "Stop"

$dotnetHome = Join-Path $HOME ".dotnet"
if ((Test-Path (Join-Path $dotnetHome "dotnet.exe")) -and -not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
    $env:PATH = "$dotnetHome;$env:PATH"
}

$projectRoot = Split-Path -Parent $PSScriptRoot
$solutionPath = Join-Path $projectRoot "ProsoftAutoLogin.sln"

Write-Host "Restoring packages..."
dotnet restore $solutionPath

Write-Host "Building Release..."
dotnet build $solutionPath --configuration Release --no-restore

Write-Host "Build completed."
