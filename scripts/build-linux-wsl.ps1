# Build the Avalonia (Linux) project inside WSL — no local Linux install required.
# Requires: WSL2 + Ubuntu + .NET 8 SDK in WSL (see docs/MULTIPLATFORM.md)

$ErrorActionPreference = "Stop"
$root = Split-Path $PSScriptRoot -Parent

$linuxPath = (wsl wslpath -a $root 2>$null)
if (-not $linuxPath) {
    Write-Error "WSL not available. Install with: wsl --install"
}

Write-Host "Building Avalonia app in WSL: $linuxPath"
wsl bash -lc "cd '$linuxPath' && dotnet build PokeMMOTracker.Avalonia/PokeMMOTracker.Avalonia.csproj -c Release"
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

Write-Host ""
Write-Host "OK. To run with GUI (WSLg on Windows 11):"
Write-Host "  wsl bash -lc \"cd '$linuxPath' && dotnet run --project PokeMMOTracker.Avalonia -c Release --no-build\""
