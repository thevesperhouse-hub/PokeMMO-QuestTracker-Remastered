using System;
using System.Collections.Generic;
using System.Data.SQLite;
using System.IO;
using System.Reflection;
using PokeMMOTracker.Models;

namespace PokeMMOTracker;

public static class DatabaseHelper
{
	public static string GetDatabasePath()
	{
		string folderPath = "";
		folderPath = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
		folderPath = Path.Combine(folderPath, "PokeMMOTracker");
		if (!Directory.Exists(folderPath))
		{
			Directory.CreateDirectory(folderPath);
		}
		return Path.Combine(folderPath, "PokeMMODb.db");
	}

	public static ShowUserProgress GetUserProgress(string dbPath, string charName, string regionName)
	{
		string region = "";
		switch (regionName)
		{
		case "Kanto":
			region = "KantoProgress";
			break;
		case "Johto":
			region = "JohtoProgress";
			break;
		case "Hoenn":
			region = "HoennProgress";
			break;
		case "Sinnoh":
			region = "SinnohProgress";
			break;
		case "Unova":
			region = "UnovaProgress";
			break;
		}
		using SQLiteConnection connection = new SQLiteConnection("Data Source=" + dbPath + ";Version=3;");
		List<(string, int)> list = new List<(string, int)>();
		ShowUserProgress showUserProgress = new ShowUserProgress();
		connection.Open();
		using (SQLiteCommand command = new SQLiteCommand("SELECT " + region + " FROM UserClass WHERE Name = @charName;", connection))
		{
			command.Parameters.AddWithValue("@charName", charName);
			using SQLiteDataReader reader = command.ExecuteReader();
			while (reader.Read())
			{
				showUserProgress.RegionId = reader.GetInt32(0);
			}
		}
		using (SQLiteCommand command2 = new SQLiteCommand($"SELECT {(AppConfig.Language == "FR" ? "Title_FR" : "Title")} FROM {regionName}Class WHERE {regionName}Id = @Progress;", connection))
		{
			command2.Parameters.AddWithValue("@Progress", showUserProgress.RegionId);
			using SQLiteDataReader reader2 = command2.ExecuteReader();
			while (reader2.Read())
			{
				try { showUserProgress.Title = reader2.GetString(0); }
				catch { showUserProgress.Title = "Missing Translation"; }
			}
		}
		
		string labelColumn = AppConfig.Language == "FR" ? "TaskLabel_FR" : "TaskLabel";
		
		// Auto-heal missing user columns
		try 
		{
			using (SQLiteCommand checkCmd = new SQLiteCommand($"SELECT {charName}IsDone FROM {region}Class LIMIT 1;", connection))
			{
				checkCmd.ExecuteScalar();
			}
		} 
		catch (SQLiteException) 
		{
			// Column is missing, create it dynamically
			using (SQLiteCommand alterCmd = new SQLiteCommand($"ALTER TABLE {region}Class ADD COLUMN {charName}IsDone INTEGER DEFAULT 0;", connection))
			{
				alterCmd.ExecuteNonQuery();
			}
		}

		using (SQLiteCommand command3 = new SQLiteCommand($"SELECT {labelColumn}, {charName}IsDone FROM {region}Class WHERE {regionName}Id = @Progress;", connection))
		{
			command3.Parameters.AddWithValue("@Progress", showUserProgress.RegionId);
			using SQLiteDataReader reader3 = command3.ExecuteReader();
			while (reader3.Read())
			{
				string label;
				try { label = reader3.IsDBNull(0) ? "Missing Translation" : reader3.GetString(0); }
				catch { label = "Missing Translation"; }
				
				int isDone = reader3.GetInt32(1);
				list.Add((label, isDone));
			}
		}
		showUserProgress.labels = list;
		return showUserProgress;
	}

	public static int GetMaxRegionId(string dbPath, string regionName)
	{
		string regionTable = regionName + "Class";
		string regionIdColumn = regionName + "Id";
		using SQLiteConnection connection = new SQLiteConnection("Data Source=" + dbPath + ";Version=3;");
		connection.Open();
		using SQLiteCommand command = new SQLiteCommand($"SELECT MAX({regionIdColumn}) FROM {regionTable};", connection);
		object result = command.ExecuteScalar();
		if (result != DBNull.Value && result != null)
		{
			return Convert.ToInt32(result);
		}
		return 1;
	}

	public static List<string> GetAllCharacterNames(string dbPath)
	{
		List<string> names = new List<string>();
		try
		{
			using SQLiteConnection connection = new SQLiteConnection("Data Source=" + dbPath + ";Version=3;");
			connection.Open();
			using SQLiteCommand command = new SQLiteCommand("SELECT Name FROM UserClass ORDER BY Name;", connection);
			using SQLiteDataReader reader = command.ExecuteReader();
			while (reader.Read())
				names.Add(reader.GetString(0));
		}
		catch { }
		return names;
	}

	public static bool CharacterExists(string dbPath, string name)
	{
		try
		{
			using SQLiteConnection connection = new SQLiteConnection("Data Source=" + dbPath + ";Version=3;");
			connection.Open();
			using SQLiteCommand command = new SQLiteCommand("SELECT 1 FROM UserClass WHERE Name = @Name LIMIT 1;", connection);
			command.Parameters.AddWithValue("@Name", name);
			return command.ExecuteScalar() != null;
		}
		catch { return false; }
	}

	public static bool IsValidCharacterName(string name)
	{
		if (string.IsNullOrWhiteSpace(name) || name.Length > 16) return false;
		if (name.Contains(' ')) return false;
		foreach (char c in name)
			if (!char.IsLetterOrDigit(c)) return false;
		return true;
	}

	public static bool RenameCharacter(string dbPath, string oldName, string newName)
	{
		if (oldName == newName) return true;
		if (!IsValidCharacterName(newName)) return false;
		if (CharacterExists(dbPath, newName)) return false;
		if (!CharacterExists(dbPath, oldName)) return false;

		string[] progressTables = { "KantoProgressClass", "JohtoProgressClass", "HoennProgressClass", "SinnohProgressClass", "UnovaProgressClass" };
		string[] classTables = { "KantoClass", "JohtoClass", "HoennClass", "SinnohClass", "UnovaClass" };

		try
		{
			using SQLiteConnection connection = new SQLiteConnection("Data Source=" + dbPath + ";Version=3;");
			connection.Open();
			using SQLiteTransaction tx = connection.BeginTransaction();

			using (SQLiteCommand updateUser = new SQLiteCommand("UPDATE UserClass SET Name = @newName WHERE Name = @oldName;", connection, tx))
			{
				updateUser.Parameters.AddWithValue("@newName", newName);
				updateUser.Parameters.AddWithValue("@oldName", oldName);
				if (updateUser.ExecuteNonQuery() != 1)
				{
					tx.Rollback();
					return false;
				}
			}

			string oldCol = oldName + "IsDone";
			string newCol = newName + "IsDone";
			foreach (string table in progressTables)
				RenameColumnIfExists(connection, tx, table, oldCol, newCol);
			foreach (string table in classTables)
				RenameColumnIfExists(connection, tx, table, oldCol, newCol);

			tx.Commit();
			return true;
		}
		catch
		{
			return false;
		}
	}

	private static void RenameColumnIfExists(SQLiteConnection connection, SQLiteTransaction tx, string table, string oldCol, string newCol)
	{
		try
		{
			using SQLiteCommand check = new SQLiteCommand($"SELECT {oldCol} FROM {table} LIMIT 1;", connection, tx);
			check.ExecuteScalar();
		}
		catch (SQLiteException)
		{
			return;
		}

		using SQLiteCommand rename = new SQLiteCommand($"ALTER TABLE {table} RENAME COLUMN {oldCol} TO {newCol};", connection, tx);
		rename.ExecuteNonQuery();
	}

	public static CharacterDashboardInfo GetCharacterDashboardInfo(string dbPath, string charName)
	{
		CharacterDashboardInfo info = new CharacterDashboardInfo { Name = charName };
		string[] regions = { "Kanto", "Johto", "Hoenn", "Sinnoh", "Unova" };

		info.GlobalPercent = GetTotalProgressPercentage(dbPath, charName);
		foreach (string region in regions)
			info.RegionPercents[region] = GetRegionProgressPercentage(dbPath, charName, region);

		string activeRegion = CharacterPrefs.GetLastRegion(charName);
		if (!info.RegionPercents.ContainsKey(activeRegion))
			activeRegion = "Kanto";

		info.ActiveRegion = activeRegion;
		ShowUserProgress progress = GetUserProgress(dbPath, charName, activeRegion);
		info.ActiveZoneTitle = progress.Title ?? "";
		info.ActiveZoneStep = progress.RegionId;

		if (progress.labels != null)
		{
			foreach (var task in progress.labels)
			{
				if (task.Item2 != 1)
				{
					info.CurrentQuest = task.Item1;
					break;
				}
			}
			if (info.CurrentQuest == null && progress.labels.Count > 0)
				info.CurrentQuest = progress.labels[progress.labels.Count - 1].Item1;
		}

		return info;
	}

	public static double GetRegionProgressPercentage(string dbPath, string charName, string regionName)
	{
	        string regionDb = regionName + "ProgressClass";
	        string isDoneCol = charName + "IsDone";

	        try 
	        {
	                using SQLiteConnection connection = new SQLiteConnection("Data Source=" + dbPath + ";Version=3;");
	                connection.Open();
	                using SQLiteCommand countAllCmd = new SQLiteCommand($"SELECT COUNT(*) FROM {regionDb};", connection);
	                double total = Convert.ToDouble(countAllCmd.ExecuteScalar());

	                if (total == 0) return 0.0;

	                using SQLiteCommand countDoneCmd = new SQLiteCommand($"SELECT COUNT(*) FROM {regionDb} WHERE {isDoneCol} = 1;", connection);
	                double done = Convert.ToDouble(countDoneCmd.ExecuteScalar());

	                return (done / total) * 100.0;
	        }
	        catch { return 0.0; }
	}

	public static double GetTotalProgressPercentage(string dbPath, string charName)
	{
	        string[] regions = { "Kanto", "Johto", "Hoenn", "Sinnoh", "Unova" };
	        double totalAll = 0;
	        double doneAll = 0;

	        try
	        {
	                using SQLiteConnection connection = new SQLiteConnection("Data Source=" + dbPath + ";Version=3;");
	                connection.Open();

	                foreach (string region in regions)
	                {
	                        string regionDb = region + "ProgressClass";
	                        string isDoneCol = charName + "IsDone";

	                        using SQLiteCommand countAllCmd = new SQLiteCommand($"SELECT COUNT(*) FROM {regionDb};", connection);
	                        totalAll += Convert.ToDouble(countAllCmd.ExecuteScalar());

	                        using SQLiteCommand countDoneCmd = new SQLiteCommand($"SELECT COUNT(*) FROM {regionDb} WHERE {isDoneCol} = 1;", connection);
	                        doneAll += Convert.ToDouble(countDoneCmd.ExecuteScalar());
	                }

	                if (totalAll == 0) return 0.0;
	                return (doneAll / totalAll) * 100.0;
	        }
	        catch { return 0.0; }
	}

	public static void UpdateTaskStatus(string dbPath, string taskLabel, int newStatus, int progressId, string charName, string regionName, string regionDb)
	{
		string labelColumn = AppConfig.Language == "FR" ? "TaskLabel_FR" : "TaskLabel";
		using SQLiteConnection connection = new SQLiteConnection("Data Source=" + dbPath + ";Version=3;");
		connection.Open();
		using SQLiteCommand command = new SQLiteCommand($"UPDATE {regionDb}Class SET {charName}IsDone = @isDone WHERE {regionName}Id = @Progress AND {labelColumn} = @TaskLabel;", connection);
		command.Parameters.AddWithValue("@isDone", newStatus);
		command.Parameters.AddWithValue("@Progress", progressId);
		command.Parameters.AddWithValue("@TaskLabel", taskLabel);
		command.ExecuteNonQuery();
	}

	public static bool CheckAndAdvanceProgress(string dbPath, string charName, string regionName, int progressId, string regionDb)
	{
		bool advanced = false;
		using (SQLiteConnection connection = new SQLiteConnection("Data Source=" + dbPath + ";Version=3;"))
		{
			connection.Open();
			using SQLiteCommand command = new SQLiteCommand($"SELECT COUNT(*) FROM {regionDb}Class WHERE {regionName}Id = @progressId AND {charName}IsDone = 0;", connection);
			command.Parameters.AddWithValue("@progressId", progressId);
			if (Convert.ToInt32(command.ExecuteScalar()) == 0)
			{
				int maxRegionId = GetMaxRegionId(dbPath, regionName);
				if (progressId < maxRegionId)
				{
					int newRegionId = progressId + 1;
					string regionColumn = regionName + "Progress";
					using (SQLiteCommand updateCommand = new SQLiteCommand("UPDATE UserClass SET " + regionColumn + " = @newRegionId WHERE Name = @charName;", connection))
					{
						updateCommand.Parameters.AddWithValue("@newRegionId", newRegionId);
						updateCommand.Parameters.AddWithValue("@charName", charName);
						updateCommand.ExecuteNonQuery();
					}
					advanced = true;
				}
			}
		}
		return advanced;
	}

	public static void InsertUser(string dbPath, string name, int kanto, int johto, int hoenn, int sinnoh, int unova)
	{
		using SQLiteConnection connection = new SQLiteConnection("Data Source=" + dbPath + ";Version=3;");
		connection.Open();
		using (SQLiteCommand command = new SQLiteCommand("SELECT * FROM UserClass WHERE Name = @Name;", connection))
		{
			command.Parameters.AddWithValue("@Name", name);
			using SQLiteDataReader reader = command.ExecuteReader();
			if (reader.HasRows)
			{
				return;
			}
		}
		using (SQLiteCommand command2 = new SQLiteCommand("\r\n                INSERT INTO UserClass (Name, KantoProgress, JohtoProgress, HoennProgress, SinnohProgress, UnovaProgress)\r\n                VALUES (@Name, @Kanto, @Johto, @Hoenn, @Sinnoh, @Unova);", connection))
		{
			command2.Parameters.AddWithValue("@Name", name);
			command2.Parameters.AddWithValue("@Kanto", kanto);
			command2.Parameters.AddWithValue("@Johto", johto);
			command2.Parameters.AddWithValue("@Hoenn", hoenn);
			command2.Parameters.AddWithValue("@Sinnoh", sinnoh);
			command2.Parameters.AddWithValue("@Unova", unova);
			command2.ExecuteNonQuery();
		}
		string[] array = new string[5]
		{
			"ALTER TABLE KantoProgressClass ADD COLUMN " + name + "IsDone INTEGER DEFAULT 0",
			"ALTER TABLE JohtoProgressClass ADD COLUMN " + name + "IsDone INTEGER DEFAULT 0",
			"ALTER TABLE HoennProgressClass ADD COLUMN " + name + "IsDone INTEGER DEFAULT 0",
			"ALTER TABLE SinnohProgressClass ADD COLUMN " + name + "IsDone INTEGER DEFAULT 0",
			"ALTER TABLE UnovaProgressClass ADD COLUMN " + name + "IsDone INTEGER DEFAULT 0"
		};
		for (int i = 0; i < array.Length; i++)
		{
			try 
			{
				using SQLiteCommand command3 = new SQLiteCommand(array[i], connection);
				command3.ExecuteNonQuery();
			}
			catch (SQLiteException)
			{
				// Ignore duplicate column exception if it already exists from a legacy save
			}
		}
	}

	public static void EnsureDatabaseExists()
	{
		string dbPath = GetDatabasePath();
		if (File.Exists(dbPath))
		{
			return;
		}
		string resourceName = "PokeMMOTracker.Resources.PokeMMODb.db";
		using Stream resourceStream = Assembly.GetExecutingAssembly().GetManifestResourceStream(resourceName);
		if (resourceStream == null)
		{
			throw new Exception("Embedded resource '" + resourceName + "' not found.");
		}
		using FileStream fileStream = new FileStream(dbPath, FileMode.Create, FileAccess.Write);
		resourceStream.CopyTo(fileStream);
	}
}
