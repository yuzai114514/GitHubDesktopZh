# Build script for GitHubDesktopZh
param(
    [string]$Configuration = "Release",
    [string]$OutputDir = "artifacts"
)

$ErrorActionPreference = "Stop"

Write-Host "Building GitHubDesktopZh..." -ForegroundColor Cyan

# Clean
if (Test-Path $OutputDir) {
    Remove-Item -Recurse -Force $OutputDir
}
New-Item -ItemType Directory -Path $OutputDir -Force | Out-Null

# Build solution
dotnet build -c $Configuration --nologo
if ($LASTEXITCODE -ne 0) { throw "Build failed" }

# Publish App as single-file win-x64
$publishDir = Join-Path $OutputDir "publish"
dotnet publish src/GitHubDesktopZh.App/GitHubDesktopZh.App.csproj -c $Configuration -r win-x64 --self-contained true /p:PublishSingleFile=true /p:IncludeNativeLibrariesForSelfExtract=true --nologo -o $publishDir
if ($LASTEXITCODE -ne 0) { throw "Publish failed" }

# Run tests
dotnet test --nologo
if ($LASTEXITCODE -ne 0) { throw "Tests failed" }

$exePath = Join-Path $publishDir "GitHubDesktopZh.App.exe"
$size = (Get-Item $exePath).Length / 1MB

Write-Host ""
Write-Host "Build completed!" -ForegroundColor Green
Write-Host "  Exe: $exePath" -ForegroundColor Yellow
Write-Host "  Size: $([math]::Round($size, 1)) MB" -ForegroundColor Yellow
Write-Host ""
Write-Host "To create installer, run: iscc setup\GitHubDesktopZh.iss" -ForegroundColor Cyan