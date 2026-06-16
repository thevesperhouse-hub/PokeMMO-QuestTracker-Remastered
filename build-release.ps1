# Publish single-file portable — output: PokeMMO-QT.exe (+ SDL2.dll)
$ErrorActionPreference = "Stop"
$root = $PSScriptRoot
$publishDir = Join-Path $root "publish"
$outExe = Join-Path $root "PokeMMO-QT.exe"
$sdlSource = Join-Path $root "SDL2\SDL2.dll"

if (-not (Test-Path $sdlSource)) { Write-Error "SDL2.dll missing: $sdlSource" }

& (Join-Path $root "prepare-icon.ps1")

if (Test-Path (Join-Path $root "bin")) { Remove-Item (Join-Path $root "bin") -Recurse -Force }
if (Test-Path (Join-Path $root "obj")) { Remove-Item (Join-Path $root "obj") -Recurse -Force }
if (Test-Path $publishDir) { Remove-Item $publishDir -Recurse -Force }

Write-Host "Publishing single-file..."
dotnet publish "$root\PokeMMOTracker.csproj" -c Release -r win-x64 --self-contained true `
    -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true `
    -o $publishDir

$published = Join-Path $publishDir "PokeMMOTracker.exe"
if (-not (Test-Path $published)) { Write-Error "Publish failed: $published not found" }

Copy-Item $published $outExe -Force
Remove-Item (Join-Path $root "PokeMMOTracker.exe") -Force -ErrorAction SilentlyContinue

Copy-Item $sdlSource $root -Force
Copy-Item $sdlSource $publishDir -Force

Write-Host ""
Write-Host "OK -> $outExe ($((Get-Item $outExe).Length) bytes)"
Write-Host "SDL2 -> $(Join-Path $root 'SDL2.dll')"
