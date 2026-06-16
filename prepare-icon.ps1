# Normalise app.ico (multi-resolution) for MSBuild + embed_exe_icon.
$ErrorActionPreference = "Stop"
$root = $PSScriptRoot
$iconDest = Join-Path $root "app.ico"
$assetsIcon = Join-Path $root "Assets\app.ico"
$normalizeScript = Join-Path $root "normalize_icon.py"

$iconSource = $assetsIcon
if (-not (Test-Path $iconSource)) {
    $iconSource = $iconDest
}
if (-not (Test-Path $iconSource)) {
    Write-Error "Icon not found (expected Assets\app.ico or app.ico)"
}

$python = Get-Command python -ErrorAction SilentlyContinue
if (-not $python) {
    Copy-Item $iconSource $iconDest -Force
    Write-Warning "python absent: copie brute (icone peut etre incomplete)."
    return
}

python $normalizeScript $iconSource $iconDest
if ($LASTEXITCODE -ne 0) { Write-Error "Icon normalization failed (pip install pillow)" }

Copy-Item $iconDest $assetsIcon -Force
