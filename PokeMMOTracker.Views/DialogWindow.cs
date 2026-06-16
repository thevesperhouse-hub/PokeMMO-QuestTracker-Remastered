using System;

using System.Windows;

using System.Windows.Controls;

using System.Windows.Markup;

using System.Windows.Media;
using System.Windows.Media.Imaging;

using PokeMMOTracker;

using PokeMMOTracker.Properties;



namespace PokeMMOTracker.Views;



public partial class DialogWindow : Window

{

	private static readonly string[] Regions = { "Kanto", "Johto", "Hoenn", "Sinnoh", "Unova" };

	private string _selectedRegion = "";

	private readonly FontFamily _poppins = new FontFamily(new Uri("pack://application:,,,/"), "./Fonts/#Poppins");



	public DialogWindow()

	{

		InitializeComponent();

		Icon = BitmapFrame.Create((BitmapSource)AppAssets.AppIcon);

		ApplyLocalization();

		BuildRegionChips();

	}



	private void ApplyLocalization()

	{

		Title = Loc.DialogWindowTitle;

		TitleText.Text = Loc.CreateCharacterTitle;

		NameLabel.Text = Loc.CharacterNameLabel;

		RegionLabel.Text = Loc.StartingRegionLabel;

		ContinueBtn.Content = Loc.Continue;

		BackBtn.Content = Loc.Back;

	}



	private void BuildRegionChips()

	{

		RegionChips.Children.Clear();

		foreach (string region in Regions)

		{

			bool selected = region == _selectedRegion;

			Button chip = new Button

			{

				Content = Loc.RegionDisplayName(region),

				Tag = region,

				FontSize = 11,

				Padding = new Thickness(12, 6, 12, 6),

				Margin = new Thickness(3),

				Cursor = System.Windows.Input.Cursors.Hand,

				FontFamily = _poppins,

				Foreground = Brushes.White,

				BorderThickness = new Thickness(1),

				Background = selected

					? new SolidColorBrush(Color.FromArgb(0xCC, 0x3D, 0x6B, 0x9E))

					: new SolidColorBrush(Color.FromArgb(0x88, 0x28, 0x28, 0x28)),

				BorderBrush = selected

					? new SolidColorBrush(Color.FromArgb(0xCC, 0x6A, 0x9F, 0xD4))

					: new SolidColorBrush(Color.FromArgb(0x88, 0x55, 0x55, 0x55))

			};

			chip.Resources.Add(typeof(Border), new Style(typeof(Border))

			{

				Setters = { new Setter(Border.CornerRadiusProperty, new CornerRadius(6)) }

			});

			chip.Click += RegionChip_Click;

			RegionChips.Children.Add(chip);

		}

	}



	private void RegionChip_Click(object sender, RoutedEventArgs e)

	{

		if (sender is Button btn && btn.Tag is string region)

		{

			_selectedRegion = region;

			BuildRegionChips();

		}

	}



	private void Button_Click(object sender, RoutedEventArgs e)

	{

		if (string.IsNullOrEmpty(_selectedRegion))

		{

			MessageBox.Show(Loc.ErrSelectRegion, Loc.DialogWindowTitle, MessageBoxButton.OK, MessageBoxImage.Warning);

			return;

		}

		if (string.IsNullOrWhiteSpace(CharacterNameTB.Text))

		{

			MessageBox.Show(Loc.ErrNameRequired, Loc.DialogWindowTitle, MessageBoxButton.OK, MessageBoxImage.Warning);

			return;

		}

		if (CharacterNameTB.Text.Contains(" "))

		{

			MessageBox.Show(Loc.ErrNameInvalid, Loc.DialogWindowTitle, MessageBoxButton.OK, MessageBoxImage.Warning);

			return;

		}



		string name = CharacterNameTB.Text.Trim();

		DatabaseHelper.InsertUser(DatabaseHelper.GetDatabasePath(), name, 1, 1, 1, 1, 1);



		Settings.Default.LastUser = name;

		Settings.Default.LastRegion = _selectedRegion;

		Settings.Default.Save();



		CharacterPrefs.SetLastRegion(name, _selectedRegion);



		new MainWindow(name, _selectedRegion).Show();

		Close();

	}



	private void BackBtn_Click(object sender, RoutedEventArgs e)

	{

		new LoginWindow().Show();

		Close();

	}

}


