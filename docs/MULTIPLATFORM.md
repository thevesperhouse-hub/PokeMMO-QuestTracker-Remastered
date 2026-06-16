# PokeMMO-QT — multi-plateforme (Linux first)

## Situation actuelle

| Composant | Windows (WPF) | Linux |
|-----------|-----------------|-------|
| UI | WPF (`PokeMMOTracker.csproj`) | **À faire** — Avalonia |
| Logique quêtes / SQLite | `DatabaseHelper`, models | Réutilisable (après migration SQLite) |
| Manette SDL2 | Oui | Oui (SDL2 natif Linux) |
| Overlay borderless | `user32` Win32 | X11 / Wayland (différent) |
| Narrateur Edge | SayIt | SayIt (WebSocket, cross-platform) |
| Narrateur Windows | System.Speech | `speech-dispatcher` / Piper (plus tard) |
| Hotkeys globaux | NHotkey | X11/Wayland (phase ultérieure) |

**WPF ne tourne pas sur Linux.** On garde l’app Windows actuelle et on ajoute un **second frontend** Avalonia qui partagera le même « core ».

## Architecture cible

```
PokeMMOTracker.Core/          # net8.0 — DB, Loc, prefs, modèles (sans UI)
PokeMMOTracker.Windows/     # WPF actuel (rename progressif)
PokeMMOTracker.Avalonia/    # Linux + Windows + macOS (même UI cross-platform)
```

Phase actuelle : **squelette Avalonia** + CI Linux. Le core n’est pas encore extrait — le Windows build reste inchangé.

## Tester Linux depuis Windows (sans VM dédiée)

### Option A — WSL2 + WSLg (recommandé, Windows 11)

1. PowerShell admin : `wsl --install` (Ubuntu)
2. Redémarrer, ouvrir **Ubuntu**
3. Installer le SDK :

```bash
wget https://dot.net/v1/dotnet-install.sh -O dotnet-install.sh
chmod +x dotnet-install.sh
./dotnet-install.sh --channel 8.0
echo 'export DOTNET_ROOT=$HOME/.dotnet' >> ~/.bashrc
echo 'export PATH=$HOME/.dotnet:$PATH' >> ~/.bashrc
source ~/.bashrc
```

4. Dans le repo :

```bash
cd /mnt/c/Users/boeri/Desktop/GeminiCLI/Agent/win-x64
dotnet build PokeMMOTracker.Avalonia/PokeMMOTracker.Avalonia.csproj -c Release
dotnet run --project PokeMMOTracker.Avalonia -c Release --no-build
```

Avec **WSLg**, la fenêtre Avalonia s’affiche sur Windows comme une app normale.

Script Windows : `.\scripts\build-linux-wsl.ps1`

### Option B — GitHub Actions

Chaque push compile sur `ubuntu-latest` (voir `.github/workflows/linux-build.yml`). Tu vois les erreurs sans Linux local.

### Option C — VM

VirtualBox + Ubuntu Desktop — utile pour tester overlay / manette sous un vrai desktop Linux.

## Plan de port (ordre recommandé)

1. **Squelette Avalonia** — fenêtre, thème sombre, build Linux CI ✅ (ce commit)
2. **Extraire `PokeMMOTracker.Core`** — DatabaseHelper, Loc, CharacterPrefs, TrackerLog
3. **SQLite cross-platform** — migrer `System.Data.SQLite` → `Microsoft.Data.Sqlite` (plus simple sur Linux)
4. **Hub persos** — cartes + création perso (Avalonia)
5. **Tracker overlay** — liste quêtes, progression, checkboxes
6. **SDL2 manette** — réutiliser la logique du `MainWindow.cs` (sans Win32)
7. **Narrateur** — SayIt en priorité ; fallback Linux plus tard
8. **Overlay Linux** — position always-on-top (X11 `_NET_WM_STATE_ABOVE` / Wayland limitations)
9. **Packaging** — `.deb` / Flatpak / AppImage

## Limites Linux à anticiper

- **Always-on-top** : moins uniforme que Windows (Wayland restreint plus que X11).
- **Borderless PokeMMO** : la hotkey `Ctrl+Shift+B` est Win32 — pas sur Linux (ou via outils externes).
- **Taille du port** : ~70 % logique réutilisable, **UI à reconstruire** en Avalonia (~2–4 semaines pour un MVP hub + tracker).

## Builds

| Cible | Commande |
|-------|----------|
| Windows (actuel) | `.\build.ps1` |
| Linux (Avalonia) | `dotnet build PokeMMOTracker.Avalonia -c Release` |
| Linux via WSL | `.\scripts\build-linux-wsl.ps1` |
