using System.Collections.Generic;
using System.Data.SQLite;
using System.Windows;
using System.Windows.Markup;
using PokeMMOTracker.Properties;

namespace PokeMMOTracker.Views;

public partial class LoginWindow : Window, IComponentConnector
{
	public LoginWindow()
	{
		InitializeComponent();
		RegionComboBox.Items.Add("Select Region");
		RegionComboBox.Items.Add("Kanto");
		RegionComboBox.Items.Add("Johto");
		RegionComboBox.Items.Add("Hoenn");
		RegionComboBox.Items.Add("Sinnoh");
		RegionComboBox.Items.Add("Unova");
		RegionComboBox.SelectedIndex = 0;
		UserComboBox.Items.Add("Create new character");
		foreach (string item in FillUsersComboBox(DatabaseHelper.GetDatabasePath()))
		{
			UserComboBox.Items.Add(item);
		}
		UserComboBox.SelectedIndex = 0;
		if (Settings.Default.RememberMe && Settings.Default.LastUser != "" && Settings.Default.LastRegion != "")
		{
			new MainWindow(Settings.Default.LastUser, Settings.Default.LastRegion).Show();
			Close();
		}
	}

	private static List<string> FillUsersComboBox(string dbPath)
	{
		List<string> list = new List<string>();
		using SQLiteConnection connection = new SQLiteConnection("Data Source=" + dbPath + ";Version=3;");
		connection.Open();
		using SQLiteCommand command = new SQLiteCommand("SELECT Name FROM UserClass;", connection);
		using SQLiteDataReader reader = command.ExecuteReader();
		while (reader.Read())
		{
			string name = reader.GetString(0);
			list.Add(name);
		}
		return list;
	}

	private void StartBtn_Click(object sender, RoutedEventArgs e)
	{
		if (UserComboBox.SelectedIndex == 0)
		{
			new DialogWindow().Show();
			Close();
			return;
		}
		if (RegionComboBox.SelectedIndex == 0)
		{
			MessageBox.Show("You have to select a region!");
			return;
		}
		bool scrollEnabled = RememberScroll.IsChecked == true;
		if (RememberMeCheckBox.IsChecked == true)
		{
			Settings.Default.LastUser = UserComboBox.SelectedItem.ToString();
			Settings.Default.LastRegion = RegionComboBox.SelectedItem.ToString();
			Settings.Default.RememberMe = true;
			Settings.Default.RememberScroll = scrollEnabled;
			Settings.Default.Save();
		}
		else
		{
			Settings.Default.LastUser = "";
			Settings.Default.LastRegion = "";
			Settings.Default.RememberMe = false;
			Settings.Default.RememberScroll = scrollEnabled;
			Settings.Default.Save();
		}
		new MainWindow(UserComboBox.SelectedItem.ToString(), RegionComboBox.SelectedItem.ToString()).Show();
		Close();
	}

	private void ChangeTheme_Click(object sender, RoutedEventArgs e)
	{
		new SettingsWindow().ShowDialog();
	}
}
