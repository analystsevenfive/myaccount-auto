$ErrorActionPreference = "Stop"

$dotnetHome = Join-Path $HOME ".dotnet"
if ((Test-Path (Join-Path $dotnetHome "dotnet.exe")) -and -not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
    $env:PATH = "$dotnetHome;$env:PATH"
}

$projectRoot = Split-Path -Parent $PSScriptRoot
$projectPath = Join-Path $projectRoot "src\ProsoftAutoLogin\ProsoftAutoLogin.csproj"

dotnet run --project $projectPath --configuration Debug
