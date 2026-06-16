# Met a jour l'icone puis rebuild framework-dependent.
$ErrorActionPreference = "Stop"
$root = $PSScriptRoot
& (Join-Path $root "build.ps1")
