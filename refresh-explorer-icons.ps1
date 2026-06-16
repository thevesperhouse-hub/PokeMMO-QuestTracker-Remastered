# Vide le cache d'icones de l'explorateur Windows (liste vs apercu desynchronise).
$ErrorActionPreference = "SilentlyContinue"
ie4uinit.exe -show
Write-Host "Cache icones Explorer recharge."
Write-Host "Si l'icone en liste est encore fausse, ferme le dossier et rouvre-le."
