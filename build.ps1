# Build framework-dependent — output: bin\Release\net8.0-windows\PokeMMO-QT.exe

$ErrorActionPreference = "Stop"

$root = $PSScriptRoot

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

function Try-RemoveDirectory($path) {
    if (-not (Test-Path $path)) { return }
    try {
        Remove-Item $path -Recurse -Force -ErrorAction Stop
        Write-Host "Removed $path"
    }
    catch {
        Write-Host ""
        Write-Host "WARNING: Could not fully delete $path (files may be locked)." -ForegroundColor Yellow
        Write-Host "         Close PokeMMO-QT / PokeMMOTracker and retry if the build fails." -ForegroundColor Yellow
        Write-Host "         Continuing with incremental build..." -ForegroundColor Yellow
        Write-Host ""
    }
}

& (Join-Path $root "prepare-icon.ps1")

Stop-TrackerIfRunning
Try-RemoveDirectory (Join-Path $root "bin")
Try-RemoveDirectory (Join-Path $root "obj")

dotnet build (Join-Path $root "PokeMMOTracker.csproj") -c Release
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

$outDir = Join-Path $root "bin\Release\net8.0-windows"
$builtExe = Join-Path $outDir "PokeMMOTracker.exe"
$exe = Join-Path $outDir "PokeMMO-QT.exe"

if (-not (Test-Path $builtExe)) { Write-Error "Build failed: $builtExe not found" }

$icon = Join-Path $root "app.ico"

Move-Item $builtExe $exe -Force

python (Join-Path $root "embed_exe_icon.py") $exe $icon
if ($LASTEXITCODE -ne 0) { Write-Error "embed_exe_icon.py failed" }

Remove-Item (Join-Path $outDir "PokeMMOTracker.exe") -Force -ErrorAction SilentlyContinue
Remove-Item (Join-Path $outDir "PokeMMOTracker-run.exe") -Force -ErrorAction SilentlyContinue

$sdl = Join-Path $outDir "SDL2.dll"
if (-not (Test-Path $sdl)) { Write-Error "SDL2.dll missing in $outDir" }

Write-Host ""
Write-Host "OK -> $exe ($((Get-Item $exe).Length) bytes)"
Write-Host "SDL2 -> $sdl"
Write-Host "Run from: $outDir"
