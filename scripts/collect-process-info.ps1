$ErrorActionPreference = "Stop"

$projectRoot = Split-Path -Parent $PSScriptRoot
$logDirectory = Join-Path $projectRoot "logs"
New-Item -ItemType Directory -Force -Path $logDirectory | Out-Null

$timestamp = Get-Date -Format "yyyyMMdd-HHmmss"
$outputPath = Join-Path $logDirectory "prosoft-process-$timestamp.txt"

$processes = Get-Process | Where-Object {
    $_.ProcessName -match "myAccount|Prosoft" -or
    $_.MainWindowTitle -match "myAccount|Prosoft"
}

if (-not $processes) {
    "No process matching myAccount or Prosoft was found." | Set-Content -Path $outputPath -Encoding UTF8
} else {
    $processes |
        Select-Object ProcessName, Id, MainWindowTitle, Path |
        Format-List |
        Out-String |
        Set-Content -Path $outputPath -Encoding UTF8
}

Write-Host "Saved: $outputPath"
