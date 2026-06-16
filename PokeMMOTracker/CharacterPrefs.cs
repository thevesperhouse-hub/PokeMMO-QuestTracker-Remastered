using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace PokeMMOTracker;

// Per-character tracker prefs (region, avatar) — shared DB, individual sessions.
public static class CharacterPrefs
{
	private static readonly string PrefsPath = Path.Combine(
		Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
		"PokeMMOTracker",
		"character_prefs.json");

	private sealed class PrefsData
	{
		public Dictionary<string, string> Regions { get; set; } = new();
		public Dictionary<string, string> Avatars { get; set; } = new();
	}

	public static string GetLastRegion(string charName)
	{
		var data = LoadData();
		if (data.Regions.TryGetValue(charName, out string? region) && !string.IsNullOrEmpty(region))
			return region;
		return "Kanto";
	}

	public static void SetLastRegion(string charName, string region)
	{
		var data = LoadData();
		data.Regions[charName] = region;
		SaveData(data);
	}

	public static string GetAvatar(string charName)
	{
		var data = LoadData();
		if (data.Avatars.TryGetValue(charName, out string? avatar))
			return AvatarCatalog.ResolveId(avatar);
		return AvatarCatalog.DefaultId;
	}

	public static void SetAvatar(string charName, string avatarId)
	{
		string resolved = AvatarCatalog.ResolveId(avatarId);
		var data = LoadData();
		data.Avatars[charName] = resolved;
		SaveData(data);
	}

	public static void RenameCharacter(string oldName, string newName)
	{
		if (oldName == newName) return;
		var data = LoadData();
		if (data.Regions.TryGetValue(oldName, out string? region))
		{
			data.Regions.Remove(oldName);
			data.Regions[newName] = region;
		}
		if (data.Avatars.TryGetValue(oldName, out string? avatar))
		{
			data.Avatars.Remove(oldName);
			data.Avatars[newName] = avatar;
		}
		SaveData(data);
	}

	private static PrefsData LoadData()
	{
		try
		{
			if (!File.Exists(PrefsPath)) return new PrefsData();
			string json = File.ReadAllText(PrefsPath);
			var data = JsonSerializer.Deserialize<PrefsData>(json);
			if (data?.Regions != null)
			{
				data.Regions ??= new Dictionary<string, string>();
				data.Avatars ??= new Dictionary<string, string>();
				return data;
			}

			var legacy = JsonSerializer.Deserialize<Dictionary<string, string>>(json);
			if (legacy != null)
				return new PrefsData { Regions = legacy };
		}
		catch { }

		return new PrefsData();
	}

	private static void SaveData(PrefsData data)
	{
		try
		{
			string dir = Path.GetDirectoryName(PrefsPath)!;
			if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
			File.WriteAllText(PrefsPath, JsonSerializer.Serialize(data, new JsonSerializerOptions { WriteIndented = true }));
		}
		catch { }
	}
}
