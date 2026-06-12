using System;
using System.Windows;
using System.Windows.Input;
using System.Windows.Markup;

namespace PokeMMOTracker.Views;

public partial class DialogWindow : Window, IComponentConnector
{
	public DialogWindow()
	{
		InitializeComponent();
		RegionComboBox.Items.Add("Select Region");
		RegionComboBox.Items.Add("Kanto");
		RegionComboBox.Items.Add("Johto");
		RegionComboBox.Items.Add("Hoenn");
		RegionComboBox.Items.Add("Sinnoh");
		RegionComboBox.Items.Add("Unova");
		RegionComboBox.SelectedIndex = 0;
	}

	private void Button_Click(object sender, RoutedEventArgs e)
	{
		if (RegionComboBox.SelectedIndex == 0)
		{
			Console.WriteLine("You have to choose the region!");
			return;
		}
		if (string.IsNullOrWhiteSpace(CharacterNameTB.Text))
		{
			Console.WriteLine("Your character must have a name!");
			return;
		}
		if (CharacterNameTB.Text.Contains(" "))
		{
			Console.WriteLine("Your character name must not include spaces or special characters");
			return;
		}
		DatabaseHelper.InsertUser(DatabaseHelper.GetDatabasePath(), CharacterNameTB.Text, 1, 1, 1, 1, 1);
		new MainWindow(CharacterNameTB.Text, RegionComboBox.SelectedItem.ToString()).Show();
		Close();
	}

	private void CharacterNameTB_PreviewKeyDown(object sender, KeyEventArgs e)
	{
		if (char.IsLetter((char)e.Key))
		{
			e.Handled = true;
		}
	}
}
