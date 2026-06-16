namespace PokeMMOTracker;

using System;
using System.Windows.Input;
using SDL2;

// Lightweight FR/EN strings driven by AppConfig.Language (same toggle as quest labels).
public static class Loc
{
	public static bool IsFr => AppConfig.Language == "FR";

	public static string Pick(string en, string fr) => IsFr ? fr : en;

	// Bind window
	public static string BindTitle => Pick("Shortcuts", "Raccourcis");
	public static string BindHint => Pick(
		"Click a field, then press the key or button you want.",
		"Clique sur un champ, puis appuie sur la touche ou le bouton souhaité.");
	public static string BindValidateSection => Pick("Validate next quest", "Valider la prochaine quête");
	public static string BindUndoSection => Pick("Undo last quest", "Annuler la dernière quête");
	public static string Keyboard => Pick("Keyboard", "Clavier");
	public static string Controller => Pick("Controller", "Manette");
	public static string Reset => Pick("Reset", "Réinitialiser");
	public static string Save => Pick("Save", "Enregistrer");
	public static string Close => Pick("Close", "Fermer");
	public static string PressKey => Pick("Press a key…", "Appuie sur une touche…");
	public static string PressButton => Pick("Press a button…", "Appuie sur un bouton…");
	public static string NoController => Pick("No controller detected.", "Aucune manette détectée.");

	// Main window (bind-related)
	public static string BindToolTip => Pick("Configure shortcuts (Bind)", "Configurer les raccourcis (Bind)");
	public static string HotkeyRegisterFailed => Pick(
		"Could not register this keyboard shortcut (already used by the system?).",
		"Impossible d'enregistrer ce raccourci clavier (déjà utilisé par le système ?).");

	// Controller button labels (bind display)
	public static string DPadUp => Pick("D-Pad Up", "D-Pad Haut");
	public static string DPadDown => Pick("D-Pad Down", "D-Pad Bas");
	public static string DPadLeft => Pick("D-Pad Left", "D-Pad Gauche");
	public static string DPadRight => Pick("D-Pad Right", "D-Pad Droite");
	public static string ButtonN => Pick("Button ", "Bouton ");

	// Region names shown on the progress bar (internal DB keys stay English).
	public static string RegionDisplayName(string regionKey)
	{
		if (!IsFr) return regionKey;
		switch (regionKey)
		{
			case "Kanto": return "Kanto";
			case "Johto": return "Johto";
			case "Hoenn": return "Hoenn";
			case "Sinnoh": return "Sinnoh";
			case "Unova": return "Unys";
			default: return regionKey;
		}
	}

	public static string GlobalProgress => Pick("Global", "Global");

	public static string CompactModeToolTip => Pick("Compact view", "Mode compact");
	public static string MinimalModeToolTip => Pick("Minimal view (no footer)", "Mode minimal");
	public static string FullModeToolTip => Pick("Full view", "Mode complet");
	public static string QuestCount(int done, int total) => Pick($"{done}/{total} quests", $"{done}/{total} quêtes");
	public static string QuestRemaining(int remaining) => Pick($"{remaining} left", $"{remaining} restantes");

	public static string HubTitle => Pick("Character hub", "Hub des personnages");
	public static string HubSubtitle => Pick("Pick a character and region to track", "Choisis un personnage et une région");
	public static string HubWindowTitle => Pick("PokeMMO-QT", "PokeMMO-QT");
	public static string RememberLastCharacter => Pick("Remember last character", "Mémoriser le dernier personnage");
	public static string RememberScroll => Pick("Remember scroll position", "Mémoriser la position du scroll");
	public static string LanguageLabel => Pick("Language", "Langue");

	public static string CreateCharacterTitle => Pick("Create character", "Créer un personnage");
	public static string CharacterNameLabel => Pick("Character name", "Nom du personnage");
	public static string StartingRegionLabel => Pick("Starting region", "Région de départ");
	public static string SelectRegion => Pick("Select a region…", "Choisir une région…");
	public static string Continue => Pick("Continue", "Continuer");
	public static string Back => Pick("Back", "Retour");
	public static string ErrSelectRegion => Pick("Please select a starting region.", "Choisis une région de départ.");
	public static string ErrNameRequired => Pick("Your character needs a name.", "Ton personnage doit avoir un nom.");
	public static string ErrNameInvalid => Pick("Name cannot contain spaces or special characters.", "Le nom ne peut pas contenir des espaces ou caractères spéciaux.");
	public static string DialogWindowTitle => Pick("New character", "Nouveau personnage");

	public static string NewCharacter => Pick("+ New character", "+ Nouveau personnage");
	public static string ThemeSettings => Pick("Theme", "Thème");
	public static string GlobalLabel => Pick("Global", "Global");
	public static string CurrentQuestLabel => Pick("Current quest", "Quête actuelle");
	public static string HubToolTip => Pick("Character hub", "Hub des personnages");
	public static string RefCardGlobal(string pct) => Pick($"Global {pct}%", $"Global {pct}%");
	public static string AllQuestsDone => Pick("All quests completed!", "Toutes les quêtes sont terminées !");

	public static string NarratorEnabled => Pick("Narrator (read quests aloud)", "Narrateur (lire les quêtes)");
	public static string NarratorAutoRead => Pick("Auto-read when quest changes", "Lire auto quand la quête change");
	public static string NarratorNeural => Pick("Neural voices (online, best quality)", "Voix neurales (en ligne, meilleure qualité)");
	public static string NarratorToolTip => Pick("Read current quest aloud", "Lire la quête actuelle");
	public static string NarratorStopToolTip => Pick("Stop narrator", "Arrêter le narrateur");
	public static string NarratorAllDone(string region) => Pick(
		$"All quests completed in {region}.",
		$"Toutes les quêtes sont terminées en {region}.");
	public static string NarratorVoiceHint => Pick(
		"Neural voices use Microsoft Edge TTS (internet). Offline fallback uses Windows voices — install “Natural” voices in Windows Speech settings.",
		"Les voix neurales utilisent Edge TTS (internet). Hors ligne : voix Windows — installe des voix « Naturelles » dans Paramètres → Parole.");

	// One-line bind reminder (no labels — keys only, very subtle).
	public static string BindSummaryLine(string checkKey, string undoKey, string checkPad, string undoPad)
		=> $"{checkKey} · {undoKey} · {checkPad}/{undoPad}";

	public static string FormatKeyboardBind(string keyStr, string modsStr)
	{
		Key key = Enum.TryParse<Key>(keyStr, out var k) ? k : Key.Down;
		ModifierKeys mods = Enum.TryParse<ModifierKeys>(modsStr, out var m) ? m : ModifierKeys.None;
		string s = "";
		if (mods.HasFlag(ModifierKeys.Control)) s += "Ctrl+";
		if (mods.HasFlag(ModifierKeys.Shift)) s += "Shift+";
		if (mods.HasFlag(ModifierKeys.Alt)) s += "Alt+";
		if (mods.HasFlag(ModifierKeys.Windows)) s += "Win+";
		return s + KeySymbol(key);
	}

	private static string KeySymbol(Key key)
	{
		switch (key)
		{
			case Key.Down: return "↓";
			case Key.Up: return "↑";
			case Key.Left: return "←";
			case Key.Right: return "→";
			case Key.Space: return "Space";
			case Key.Return: return "Enter";
			case Key.Escape: return "Esc";
			case Key.Tab: return "Tab";
			case Key.Back: return "Back";
			case Key.Delete: return "Del";
			case Key.Insert: return "Ins";
			case Key.Home: return "Home";
			case Key.End: return "End";
			case Key.PageUp: return "PgUp";
			case Key.PageDown: return "PgDn";
			default:
				if (key >= Key.A && key <= Key.Z) return key.ToString();
				if (key >= Key.D0 && key <= Key.D9) return ((int)key - (int)Key.D0).ToString();
				if (key >= Key.NumPad0 && key <= Key.NumPad9) return "N" + ((int)key - (int)Key.NumPad0);
				return key.ToString();
		}
	}

	public static string ControllerShortName(int button)
	{
		switch ((SDL.SDL_GameControllerButton)button)
		{
			case SDL.SDL_GameControllerButton.SDL_CONTROLLER_BUTTON_A: return "A";
			case SDL.SDL_GameControllerButton.SDL_CONTROLLER_BUTTON_B: return "B";
			case SDL.SDL_GameControllerButton.SDL_CONTROLLER_BUTTON_X: return "X";
			case SDL.SDL_GameControllerButton.SDL_CONTROLLER_BUTTON_Y: return "Y";
			case SDL.SDL_GameControllerButton.SDL_CONTROLLER_BUTTON_BACK: return "Back";
			case SDL.SDL_GameControllerButton.SDL_CONTROLLER_BUTTON_GUIDE: return "Guide";
			case SDL.SDL_GameControllerButton.SDL_CONTROLLER_BUTTON_START: return "Start";
			case SDL.SDL_GameControllerButton.SDL_CONTROLLER_BUTTON_LEFTSTICK: return "L3";
			case SDL.SDL_GameControllerButton.SDL_CONTROLLER_BUTTON_RIGHTSTICK: return "R3";
			case SDL.SDL_GameControllerButton.SDL_CONTROLLER_BUTTON_LEFTSHOULDER: return "L1";
			case SDL.SDL_GameControllerButton.SDL_CONTROLLER_BUTTON_RIGHTSHOULDER: return "R1";
			case SDL.SDL_GameControllerButton.SDL_CONTROLLER_BUTTON_DPAD_UP: return "↑";
			case SDL.SDL_GameControllerButton.SDL_CONTROLLER_BUTTON_DPAD_DOWN: return "↓";
			case SDL.SDL_GameControllerButton.SDL_CONTROLLER_BUTTON_DPAD_LEFT: return "←";
			case SDL.SDL_GameControllerButton.SDL_CONTROLLER_BUTTON_DPAD_RIGHT: return "→";
			default: return "B" + button;
		}
	}
}
