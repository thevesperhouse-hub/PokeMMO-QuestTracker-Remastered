using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace PokeMMOTracker;

// Per-character last region (shared DB, individual tracker session prefs).
public static class CharacterPrefs
{
	private static readonly string PrefsPath = Path.Combine(
		Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
		"PokeMMOTracker",
		"character_prefs.json");

	public static string GetLastRegion(string charName)
	{
		var map = Load();
		if (map.TryGetValue(charName, out string? region) && !string.IsNullOrEmpty(region))
			return region;
		return "Kanto";
	}

	public static void SetLastRegion(string charName, string region)
	{
		var map = Load();
		map[charName] = region;
		Save(map);
	}

	private static Dictionary<string, string> Load()
	{
		try
		{
			if (!File.Exists(PrefsPath)) return new Dictionary<string, string>();
			string json = File.ReadAllText(PrefsPath);
			return JsonSerializer.Deserialize<Dictionary<string, string>>(json) ?? new Dictionary<string, string>();
		}
		catch
		{
			return new Dictionary<string, string>();
		}
	}

	private static void Save(Dictionary<string, string> map)
	{
		try
		{
			string dir = Path.GetDirectoryName(PrefsPath)!;
			if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
			File.WriteAllText(PrefsPath, JsonSerializer.Serialize(map, new JsonSerializerOptions { WriteIndented = true }));
		}
		catch { }
	}
}
