$ErrorActionPreference = "Stop"

$dotnetHome = Join-Path $HOME ".dotnet"
if (Test-Path (Join-Path $dotnetHome "dotnet.exe")) {
    $env:DOTNET_ROOT = $dotnetHome
    $env:PATH = "$dotnetHome;$env:PATH"
}

$projectRoot = Split-Path -Parent $PSScriptRoot
$projectPath = Join-Path $projectRoot "src\ProsoftAutoLogin\ProsoftAutoLogin.csproj"
$distDir = Join-Path $projectRoot "dist"
$outputDir = Join-Path $distDir "ProsoftAutoLogin"
$zipPath = Join-Path $distDir "ProsoftAutoLogin-Portable.zip"

Write-Host "==========================================================" -ForegroundColor Cyan
Write-Host " Building Self-Contained Standalone Package for Windows..." -ForegroundColor Cyan
Write-Host "==========================================================" -ForegroundColor Cyan

# Stop any running instance
Get-Process -Name ProsoftAutoLogin -ErrorAction SilentlyContinue | Stop-Process -Force -ErrorAction SilentlyContinue
Start-Sleep -Milliseconds 300

# Clean old dist folder if exists
if (Test-Path $outputDir) {
    Remove-Item -Recurse -Force $outputDir
}
if (Test-Path $zipPath) {
    Remove-Item -Force $zipPath
}
New-Item -ItemType Directory -Force -Path $outputDir | Out-Null

# Publish Self-Contained Single File
Write-Host "Publishing single-file self-contained executable..." -ForegroundColor Yellow
dotnet publish $projectPath `
    --configuration Release `
    --runtime win-x64 `
    --self-contained true `
    -p:PublishSingleFile=true `
    -p:IncludeNativeLibrariesForSelfExtract=true `
    --output $outputDir

# Copy appsettings.json
$appsettingsSrc = Join-Path $projectRoot "src\ProsoftAutoLogin\appsettings.json"
Copy-Item -Force $appsettingsSrc (Join-Path $outputDir "appsettings.json")

# Copy input/input.csv
$inputDir = Join-Path $outputDir "input"
New-Item -ItemType Directory -Force -Path $inputDir | Out-Null
$csvSrc = Join-Path $projectRoot "input\input.csv"
if (Test-Path $csvSrc) {
    Copy-Item -Force $csvSrc (Join-Path $inputDir "input.csv")
}

# Copy Thai User Guide
$guideSrc = Join-Path $projectRoot "docs\USER_GUIDE_TH.txt"
if (Test-Path $guideSrc) {
    Copy-Item -Force $guideSrc (Join-Path $outputDir "README_TH.txt")
}

# Create RUN.bat
$runLines = @(
    "@echo off",
    "chcp 65001 >nul",
    "title Prosoft Auto Login & Fill",
    "cd /d ""%~dp0""",
    "echo Starting Prosoft Auto Login & Fill...",
    "start """" ""%~dp0ProsoftAutoLogin.exe"""
)
$runLines | Set-Content -Path (Join-Path $outputDir "RUN.bat") -Encoding ASCII

# Create Install-Desktop-Shortcut.bat
$shortcutLines = @(
    "@echo off",
    "chcp 65001 >nul",
    "echo Creating shortcut on Desktop...",
    "powershell -NoProfile -ExecutionPolicy Bypass -Command ""$ws = New-Object -ComObject WScript.Shell; $s = $ws.CreateShortcut([IO.Path]::Combine([Environment]::GetFolderPath('Desktop'), 'Prosoft Auto Login.lnk')); $s.TargetPath = [IO.Path]::Combine('%~dp0', 'ProsoftAutoLogin.exe'); $s.WorkingDirectory = '%~dp0'; $s.Description = 'Prosoft Auto Login & Credit Purchase'; $s.Save();""",
    "echo Shortcut created successfully on Desktop!",
    "pause"
)
$shortcutLines | Set-Content -Path (Join-Path $outputDir "Install-Desktop-Shortcut.bat") -Encoding ASCII

# Remove PDB debug symbols to keep package clean
Get-ChildItem -Path $outputDir -Filter "*.pdb" | Remove-Item -Force

# Create ZIP archive (with retry for antivirus/disk flush)
Write-Host "Creating portable ZIP package: $zipPath..." -ForegroundColor Yellow
$zipSuccess = $false
for ($i = 1; $i -le 5; $i++) {
    try {
        Start-Sleep -Seconds 1
        Compress-Archive -Path "$outputDir\*" -DestinationPath $zipPath -Force
        $zipSuccess = $true
        break
    } catch {
        Write-Host "Waiting for file access (attempt $i/5)..." -ForegroundColor Gray
        Start-Sleep -Seconds 2
    }
}
if (-not $zipSuccess) {
    Write-Warning "Could not automatically create ZIP. Files in $outputDir are ready."
}

Write-Host "==========================================================" -ForegroundColor Green
Write-Host " Standalone Portable Package Created Successfully!" -ForegroundColor Green
Write-Host " Folder: $outputDir" -ForegroundColor Green
Write-Host " ZIP:    $zipPath" -ForegroundColor Green
Write-Host "==========================================================" -ForegroundColor Green
