# Normalise app.ico pour l'embed MSBuild (PNG 256px seul = icone exe ignoree).
$ErrorActionPreference = "Stop"
$root = $PSScriptRoot
$iconSource = Join-Path (Split-Path $root -Parent) "app.ico"
$iconDest = Join-Path $root "app.ico"

if (-not (Test-Path $iconSource)) { Write-Error "Icon not found: $iconSource" }

$python = Get-Command python -ErrorAction SilentlyContinue
if (-not $python) {
    Copy-Item $iconSource $iconDest -Force
    Write-Warning "python absent: copie brute (l'icone exe peut rester l'ancienne)."
    return
}

$py = @"
from pathlib import Path
from PIL import Image

src = Path(r'$iconSource')
dst = Path(r'$iconDest')
img = Image.open(src)
img.save(
    dst,
    format='ICO',
    sizes=[(16, 16), (32, 32), (48, 48), (64, 64), (128, 128), (256, 256)],
)
print(f'Icon normalized: {dst} ({dst.stat().st_size} bytes)')
"@

$py | python -c "import sys; exec(sys.stdin.read())"
if ($LASTEXITCODE -ne 0) { Write-Error "Icon normalization failed (install: pip install pillow)" }

Copy-Item $iconDest $iconSource -Force
