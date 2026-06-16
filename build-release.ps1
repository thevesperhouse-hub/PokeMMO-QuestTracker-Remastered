# True single-file Windows release — output: publish\PokeMMO-QT.exe (one file, no SDL2.dll beside it)

$ErrorActionPreference = "Stop"

$root = $PSScriptRoot
$publishDir = Join-Path $root "publish"
$outExe = Join-Path $publishDir "PokeMMO-QT.exe"
$sdlSource = Join-Path $root "SDL2\SDL2.dll"

function Stop-TrackerIfRunning {
    $names = @("PokeMMO-QT", "PokeMMOTracker")
    foreach ($name in $names) {
        Get-Process -Name $name -ErrorAction SilentlyContinue | ForEach-Object {
            Write-Host "Closing running process: $($_.Name) (pid $($_.Id))"
            Stop-Process -Id $_.Id -Force -ErrorAction SilentlyContinue
        }
    }
    Start-Sleep -Milliseconds 400
}

if (-not (Test-Path $sdlSource)) { Write-Error "SDL2.dll missing: $sdlSource" }

& (Join-Path $root "prepare-icon.ps1")

Stop-TrackerIfRunning

if (Test-Path (Join-Path $root "bin")) {
    try { Remove-Item (Join-Path $root "bin") -Recurse -Force -ErrorAction Stop }
    catch { Write-Host "WARNING: bin folder locked — continuing publish anyway." -ForegroundColor Yellow }
}
if (Test-Path (Join-Path $root "obj")) {
    try { Remove-Item (Join-Path $root "obj") -Recurse -Force -ErrorAction Stop }
    catch { Write-Host "WARNING: obj folder locked — continuing publish anyway." -ForegroundColor Yellow }
}
if (Test-Path $publishDir) { Remove-Item $publishDir -Recurse -Force }

Write-Host "Publishing single-file (self-contained, SDL2 embedded)..."

dotnet publish "$root\PokeMMOTracker.csproj" -c Release -r win-x64 --self-contained true `
    -p:DebugType=none -p:DebugSymbols=false `
    -o $publishDir

if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

$published = Join-Path $publishDir "PokeMMOTracker.exe"
if (-not (Test-Path $published)) { Write-Error "Publish failed: $published not found" }

Move-Item $published $outExe -Force

# Do NOT run embed_exe_icon.py here — UpdateResource corrupts the .NET single-file bundle.
# Icon is embedded via <ApplicationIcon>app.ico</ApplicationIcon> during publish.

$extras = Get-ChildItem $publishDir -File | Where-Object { $_.Name -ne "PokeMMO-QT.exe" }
if ($extras.Count -gt 0) {
    Write-Host ""
    Write-Host "WARNING: extra files in publish (expected single exe only):" -ForegroundColor Yellow
    $extras | ForEach-Object { Write-Host "  - $($_.Name)" -ForegroundColor Yellow }
}

Write-Host ""
Write-Host "OK -> $outExe ($((Get-Item $outExe).Length) bytes)"
Write-Host 'Ship this one file — SDL2 is embedded and extracted to LocalAppData\PokeMMO-QT\native on first run.'
