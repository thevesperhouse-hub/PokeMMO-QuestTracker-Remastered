using System;
using System.IO;

namespace PokeMMOTracker;

// Lightweight file log for quest history and debugging (LocalAppData/PokeMMOTracker/).
public static class TrackerLog
{
	private static readonly string LogDir = Path.Combine(
		Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
		"PokeMMOTracker");

	private static readonly string DebugPath = Path.Combine(LogDir, "tracker_debug.log");
	private static readonly string HistoryPath = Path.Combine(LogDir, "quest_history.log");

	public static void Info(string message)
	{
		Write(DebugPath, "INFO", message);
	}

	public static void Error(string message)
	{
		Write(DebugPath, "ERROR", message);
	}

	public static void QuestChange(string charName, string region, string questLabel, bool completed)
	{
		string action = completed ? "+" : "-";
		string line = $"{action} [{charName}/{region}] {questLabel}";
		Write(HistoryPath, "QUEST", line);
		Write(DebugPath, "QUEST", line);
	}

	public static void ZoneChange(string charName, string region, int zoneId, string zoneTitle)
	{
		string line = $"ZONE [{charName}/{region}] step {zoneId}: {zoneTitle}";
		Write(DebugPath, "ZONE", line);
	}

	private static void Write(string path, string level, string message)
	{
		try
		{
			if (!Directory.Exists(LogDir)) Directory.CreateDirectory(LogDir);
			string entry = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} [{level}] {message}{Environment.NewLine}";
			File.AppendAllText(path, entry);
		}
		catch { }
	}
}
