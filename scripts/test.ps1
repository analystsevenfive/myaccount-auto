$ErrorActionPreference = "Stop"

$dotnetHome = Join-Path $HOME ".dotnet"
if ((Test-Path (Join-Path $dotnetHome "dotnet.exe")) -and -not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
    $env:PATH = "$dotnetHome;$env:PATH"
}

$projectRoot = Split-Path -Parent $PSScriptRoot
$testProjectPath = Join-Path $projectRoot "tests\ProsoftAutoLogin.Tests\ProsoftAutoLogin.Tests.csproj"

Write-Host "Running unit tests..."
dotnet test $testProjectPath --configuration Release
