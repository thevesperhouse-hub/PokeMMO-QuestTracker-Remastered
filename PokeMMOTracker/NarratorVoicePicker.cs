using System;
using System.Linq;
using System.Speech.Synthesis;

namespace PokeMMOTracker;

// Picks the least "robotic" installed voice — prefers OneCore / Natural / Neural over legacy Desktop SAPI.
internal static class NarratorVoicePicker
{
	public static InstalledVoice PickSapi(SpeechSynthesizer synth, string langPrefix, string preferredName = null)
	{
		var voices = synth.GetInstalledVoices().Where(v => v.Enabled).ToList();
		if (voices.Count == 0) return null;

		if (!string.IsNullOrWhiteSpace(preferredName))
		{
			var exact = voices.FirstOrDefault(v => VoiceNameMatches(v.VoiceInfo, preferredName));
			if (exact != null) return exact;
		}

		return voices
			.Where(v => MatchesLanguage(v.VoiceInfo.Culture?.Name, langPrefix))
			.OrderByDescending(v => Score(v.VoiceInfo.Name, v.VoiceInfo.Description, v.VoiceInfo.Id))
			.FirstOrDefault(v => Score(v.VoiceInfo.Name, v.VoiceInfo.Description, v.VoiceInfo.Id) > -100)
			?? voices.OrderByDescending(v => Score(v.VoiceInfo.Name, v.VoiceInfo.Description, v.VoiceInfo.Id)).FirstOrDefault();
	}

	public static int SapiRateFor(VoiceInfo info)
	{
		int score = Score(info.Name, info.Description, info.Id);
		// Legacy Desktop voices need slower delivery to sound less harsh.
		if (score < 40) return -2;
		if (score < 70) return -1;
		return 0;
	}

	public static bool IsLegacyDesktop(VoiceInfo info)
		=> Score(info.Name, info.Description, info.Id) < 40;

	// WinRT VoiceInformation — same scoring on DisplayName / Description / Id.
	public static int ScoreWinRt(string displayName, string description, string id)
		=> Score(displayName, description, id);

	public static bool MatchesLanguage(string cultureOrLanguage, string langPrefix)
	{
		if (string.IsNullOrEmpty(cultureOrLanguage)) return false;
		return cultureOrLanguage.StartsWith(langPrefix, StringComparison.OrdinalIgnoreCase)
			|| cultureOrLanguage.Contains(langPrefix, StringComparison.OrdinalIgnoreCase);
	}

	private static bool VoiceNameMatches(VoiceInfo info, string preferredName)
	{
		return Contains(info.Name, preferredName) || Contains(info.Description, preferredName);
	}

	private static int Score(string name, string description, string id)
	{
		string blob = $"{name}|{description}|{id}";
		if (string.IsNullOrWhiteSpace(blob)) return 0;

		int score = 0;

		if (Contains(blob, "Natural")) score += 120;
		if (Contains(blob, "Neural")) score += 110;
		if (Contains(blob, "OneCore")) score += 90;
		if (Contains(blob, "24khz") || Contains(blob, "24KHZ")) score += 25;
		if (Contains(blob, "16khz") || Contains(blob, "16KHZ")) score += 10;

		// Known nicer FR voices (offline natural / onecore).
		if (Contains(blob, "Denise")) score += 40;
		if (Contains(blob, "Henri")) score += 35;
		if (Contains(blob, "Paul")) score += 30;
		if (Contains(blob, "Eloise")) score += 35;
		if (Contains(blob, "Vivienne")) score += 35;

		// Known nicer EN voices.
		if (Contains(blob, "Jenny")) score += 40;
		if (Contains(blob, "Aria")) score += 40;
		if (Contains(blob, "Guy")) score += 30;
		if (Contains(blob, "Sonia")) score += 30;
		if (Contains(blob, "Libby")) score += 30;

		// Legacy robotic Desktop SAPI voices.
		if (Contains(name, "Desktop")) score -= 80;
		if (Contains(name, "Mobile")) score -= 40;
		if (Contains(blob, "David")) score -= 25;
		if (Contains(blob, "Zira")) score -= 25;
		if (Contains(blob, "Mark")) score -= 20;
		if (Contains(blob, "Sam")) score -= 20;

		// Online-only voices can stutter or fail without network.
		if (Contains(blob, "Online")) score -= 15;

		return score;
	}

	private static bool Contains(string text, string fragment)
	{
		if (string.IsNullOrEmpty(text) || string.IsNullOrEmpty(fragment)) return false;
		return text.Contains(fragment, StringComparison.OrdinalIgnoreCase);
	}
}
