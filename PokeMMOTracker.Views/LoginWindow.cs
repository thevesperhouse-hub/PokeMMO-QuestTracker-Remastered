using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using PokeMMOTracker;
using PokeMMOTracker.Models;
using PokeMMOTracker.Properties;

namespace PokeMMOTracker.Views;

// Character hub — PureRef-style cards, shared DB progress per character.
public partial class LoginWindow : Window
{
	private static readonly string[] Regions = { "Kanto", "Johto", "Hoenn", "Sinnoh", "Unova" };
	private readonly string _dbPath = DatabaseHelper.GetDatabasePath();
	private readonly FontFamily _poppins = new FontFamily(new Uri("pack://application:,,,/"), "./Fonts/#Poppins");

	public LoginWindow()
	{
		InitializeComponent();
		Icon = BitmapFrame.Create((BitmapSource)AppAssets.AppIcon);
		ApplyLocalization();
		UpdateLangButtons();
		RememberMeCheckBox.IsChecked = Settings.Default.RememberMe;
		RememberScroll.IsChecked = Settings.Default.RememberScroll;
		NarratorEnabledCheckBox.IsChecked = Settings.Default.NarratorEnabled;
		NarratorNeuralCheckBox.IsChecked = Settings.Default.NarratorNeural;
		NarratorAutoReadCheckBox.IsChecked = Settings.Default.NarratorAutoRead;
		UpdateNarratorOptionStates();
		NarratorEnabledCheckBox.Checked += delegate { UpdateNarratorOptionStates(); };
		NarratorEnabledCheckBox.Unchecked += delegate { UpdateNarratorOptionStates(); };

		Loaded += OnLoaded;
	}

	private void ApplyLocalization()
	{
		Title = Loc.HubWindowTitle;
		HubTitleText.Text = Loc.HubTitle;
		HubSubtitleText.Text = Loc.HubSubtitle;
		LangLabelText.Text = Loc.LanguageLabel;
		NewCharBtn.Content = Loc.NewCharacter;
		ChangeTheme.Content = Loc.ThemeSettings;
		RememberMeCheckBox.Content = Loc.RememberLastCharacter;
		RememberScroll.Content = Loc.RememberScroll;
		NarratorEnabledCheckBox.Content = Loc.NarratorEnabled;
		NarratorNeuralCheckBox.Content = Loc.NarratorNeural;
		NarratorAutoReadCheckBox.Content = Loc.NarratorAutoRead;
		NarratorHintText.Text = Loc.NarratorVoiceHint;
	}

	private void UpdateNarratorOptionStates()
	{
		bool on = NarratorEnabledCheckBox.IsChecked == true;
		NarratorNeuralCheckBox.IsEnabled = on;
		NarratorAutoReadCheckBox.IsEnabled = on;
		if (!on)
			NarratorAutoReadCheckBox.IsChecked = false;
	}

	private void SetLanguage(string lang)
	{
		if (AppConfig.Language == lang) return;
		AppConfig.Language = lang;
		Settings.Default.Language = lang;
		Settings.Default.Save();
		ApplyLocalization();
		UpdateLangButtons();
		BuildCharacterCards();
	}

	private void UpdateLangButtons()
	{
		bool fr = AppConfig.Language == "FR";
		LangEnBtn.Background = fr
			? new SolidColorBrush(Color.FromArgb(0x55, 0x33, 0x33, 0x33))
			: new SolidColorBrush(Color.FromArgb(0xCC, 0x3D, 0x6B, 0x9E));
		LangFrBtn.Background = fr
			? new SolidColorBrush(Color.FromArgb(0xCC, 0x3D, 0x6B, 0x9E))
			: new SolidColorBrush(Color.FromArgb(0x55, 0x33, 0x33, 0x33));
		LangEnBtn.BorderBrush = fr
			? new SolidColorBrush(Color.FromArgb(0x55, 0xFF, 0xFF, 0xFF))
			: new SolidColorBrush(Color.FromArgb(0xCC, 0x6A, 0x9F, 0xD4));
		LangFrBtn.BorderBrush = fr
			? new SolidColorBrush(Color.FromArgb(0xCC, 0x6A, 0x9F, 0xD4))
			: new SolidColorBrush(Color.FromArgb(0x55, 0xFF, 0xFF, 0xFF));
	}

	private void LangEnBtn_Click(object sender, RoutedEventArgs e) => SetLanguage("EN");
	private void LangFrBtn_Click(object sender, RoutedEventArgs e) => SetLanguage("FR");

	private void OnLoaded(object sender, RoutedEventArgs e)
	{
		try
		{
			BuildCharacterCards();
		}
		catch (Exception ex)
		{
			TrackerLog.Error("Hub BuildCharacterCards: " + ex);
			ShowHubError(Loc.Pick("Could not load character cards.", "Impossible de charger les personnages."));
		}
	}

	private void ShowHubError(string message)
	{
		CardsPanel.Children.Clear();
		CardsPanel.Children.Add(new TextBlock
		{
			Text = message,
			Foreground = new SolidColorBrush(Color.FromArgb(0xCC, 0xFF, 0x88, 0x88)),
			Margin = new Thickness(20),
			FontSize = 13,
			TextWrapping = TextWrapping.Wrap
		});
	}

	private void BuildCharacterCards()
	{
		CardsPanel.Children.Clear();
		List<string> names = DatabaseHelper.GetAllCharacterNames(_dbPath);

		if (names.Count == 0)
		{
			CardsPanel.Children.Add(new TextBlock
			{
				Text = Loc.Pick("No characters yet — create one!", "Aucun personnage — crée-en un !"),
				Foreground = new SolidColorBrush(Color.FromArgb(0x99, 0xDD, 0xDD, 0xDD)),
				Margin = new Thickness(20),
				FontSize = 13
			});
			return;
		}

		foreach (string name in names)
		{
			try
			{
				CharacterDashboardInfo info = DatabaseHelper.GetCharacterDashboardInfo(_dbPath, name);
				bool isLastUsed = name == Settings.Default.LastUser;
				CardsPanel.Children.Add(BuildCharacterCard(info, isLastUsed));
			}
			catch (Exception ex)
			{
				TrackerLog.Error($"Hub card '{name}': " + ex);
				CardsPanel.Children.Add(BuildErrorCard(name));
			}
		}
	}

	private Border BuildErrorCard(string name)
	{
		return new Border
		{
			Width = 300,
			Margin = new Thickness(8),
			Padding = new Thickness(14),
			CornerRadius = new CornerRadius(12),
			Background = new SolidColorBrush(Color.FromArgb(0x44, 0x40, 0x10, 0x10)),
			BorderBrush = new SolidColorBrush(Color.FromArgb(0x55, 0xFF, 0x66, 0x66)),
			BorderThickness = new Thickness(1),
			Child = new TextBlock
			{
				Text = Loc.Pick($"Could not load {name}", $"Impossible de charger {name}"),
				Foreground = new SolidColorBrush(Color.FromArgb(0xCC, 0xFF, 0xAA, 0xAA)),
				TextWrapping = TextWrapping.Wrap
			}
		};
	}

	private Border BuildCharacterCard(CharacterDashboardInfo info, bool highlightLastUsed = false)
	{
		const double cardWidth = 300;
		const double bodyPad = 10;
		bool showAvatar = AppAssets.HasHubAvatar(info.Name);
		double textColumnWidth = showAvatar ? cardWidth - bodyPad * 2 - 52 - 8 : cardWidth - bodyPad * 2;

		Border card = new Border
		{
			Width = cardWidth,
			Margin = new Thickness(8),
			CornerRadius = new CornerRadius(12),
			Background = new SolidColorBrush(Color.FromArgb(0x66, 0x1A, 0x1A, 0x1A)),
			BorderBrush = highlightLastUsed
				? new SolidColorBrush(Color.FromArgb(0xCC, 0xA0, 0xD0, 0xFF))
				: new SolidColorBrush(Color.FromArgb(0x55, 0xFF, 0xFF, 0xFF)),
			BorderThickness = highlightLastUsed ? new Thickness(2) : new Thickness(1),
			Cursor = Cursors.Hand,
			ClipToBounds = true,
			Padding = new Thickness(bodyPad)
		};

		StackPanel stack = new StackPanel();

		stack.Children.Add(new TextBlock
		{
			Text = info.Name,
			FontSize = 16,
			FontWeight = FontWeights.Bold,
			Foreground = Brushes.White,
			FontFamily = _poppins
		});

		stack.Children.Add(new TextBlock
		{
			Text = Loc.RefCardGlobal($"{SafePercent(info.GlobalPercent):F1}"),
			FontSize = 10,
			Foreground = new SolidColorBrush(Color.FromArgb(0xCC, 0xA0, 0xD0, 0xFF)),
			Margin = new Thickness(0, 2, 0, 6)
		});

		stack.Children.Add(CreateHorizontalBar(
			info.GlobalPercent, textColumnWidth, 6, new CornerRadius(3), 0xCC));

		foreach (string region in Regions)
		{
			double pct = info.RegionPercents.TryGetValue(region, out double p) ? p : 0;
			stack.Children.Add(BuildRegionRow(region, pct, info.ActiveRegion == region));
		}

		string regionDisplay = Loc.RegionDisplayName(info.ActiveRegion ?? "Kanto");
		string zoneTitle = info.ActiveZoneTitle ?? "";
		stack.Children.Add(new TextBlock
		{
			Text = $"{regionDisplay} · {zoneTitle}",
			FontSize = 10,
			Foreground = new SolidColorBrush(Color.FromArgb(0x99, 0xDD, 0xDD, 0xDD)),
			Margin = new Thickness(0, 8, 0, 2),
			TextWrapping = TextWrapping.Wrap
		});

		if (!string.IsNullOrEmpty(info.CurrentQuest))
		{
			stack.Children.Add(new TextBlock
			{
				Text = $"► {info.CurrentQuest}",
				FontSize = 10,
				Foreground = new SolidColorBrush(Color.FromArgb(0xBB, 0xDD, 0xDD, 0xDD)),
				TextWrapping = TextWrapping.Wrap,
				MaxHeight = 36
			});
		}

		WrapPanel chips = new WrapPanel { Margin = new Thickness(0, 6, 0, 0) };
		foreach (string region in Regions)
		{
			Button chip = new Button
			{
				Content = Loc.RegionDisplayName(region),
				FontSize = 8,
				Padding = new Thickness(6, 3, 6, 3),
				Margin = new Thickness(2),
				Background = new SolidColorBrush(Color.FromArgb(0x88, 0x28, 0x28, 0x28)),
				Foreground = Brushes.White,
				BorderBrush = new SolidColorBrush(Color.FromArgb(0x88, 0x55, 0x55, 0x55)),
				BorderThickness = new Thickness(1),
				Cursor = Cursors.Hand,
				FontFamily = _poppins
			};
			chip.Click += delegate
			{
				OpenTracker(info.Name, region);
			};
			chips.Children.Add(chip);
		}
		stack.Children.Add(chips);

		if (showAvatar)
		{
			Grid body = new Grid();
			body.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(52) });
			body.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

			Border avatarBorder = new Border
			{
				Width = 52,
				Height = 52,
				CornerRadius = new CornerRadius(8),
				ClipToBounds = true,
				VerticalAlignment = VerticalAlignment.Top,
				Margin = new Thickness(0, 0, 8, 0),
				BorderBrush = new SolidColorBrush(Color.FromArgb(0x55, 0xFF, 0xFF, 0xFF)),
				BorderThickness = new Thickness(1),
				Child = new Image
				{
					Source = AppAssets.AvatarFull,
					Stretch = Stretch.UniformToFill
				}
			};
			Grid.SetColumn(avatarBorder, 0);
			Grid.SetColumn(stack, 1);
			body.Children.Add(avatarBorder);
			body.Children.Add(stack);
			card.Child = body;
		}
		else
		{
			card.Child = stack;
		}

		card.MouseLeftButtonDown += delegate (object s, MouseButtonEventArgs e)
		{
			if (e.OriginalSource is Button) return;
			OpenTracker(info.Name, CharacterPrefs.GetLastRegion(info.Name));
		};

		return card;
	}

	private static double SafePercent(double value)
	{
		if (double.IsNaN(value) || double.IsInfinity(value)) return 0;
		return Math.Clamp(value, 0, 100);
	}

	// Star-column fill bar — never sets a negative Width.
	private static Grid CreateHorizontalBar(double percent, double width, int height, CornerRadius radius, byte fillAlpha)
	{
		double p = SafePercent(percent);
		Grid outer = new Grid { Height = height, Width = width, Margin = new Thickness(0, 0, 0, 12) };
		outer.Children.Add(new Border
		{
			CornerRadius = radius,
			Background = new SolidColorBrush(Color.FromArgb(0x66, 0x50, 0x50, 0x50))
		});

		if (p > 0)
		{
			Grid split = new Grid();
			split.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(p, GridUnitType.Star) });
			split.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(100 - p, GridUnitType.Star) });
			Border fill = new Border
			{
				CornerRadius = radius,
				Background = new SolidColorBrush(Color.FromArgb(fillAlpha, 0x64, 0xC8, 0x64))
			};
			Grid.SetColumn(fill, 0);
			split.Children.Add(fill);
			outer.Children.Add(split);
		}

		return outer;
	}

	private Grid BuildRegionRow(string region, double pct, bool isActive)
	{
		double safePct = SafePercent(pct);
		Grid row = new Grid { Margin = new Thickness(0, 2, 0, 2) };
		row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(52) });
		row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
		row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(36) });

		TextBlock label = new TextBlock
		{
			Text = Loc.RegionDisplayName(region),
			FontSize = 9,
			Foreground = isActive ? new SolidColorBrush(Color.FromArgb(0xFF, 0xA0, 0xD0, 0xFF)) : new SolidColorBrush(Color.FromArgb(0x88, 0xDD, 0xDD, 0xDD)),
			VerticalAlignment = VerticalAlignment.Center
		};
		Grid.SetColumn(label, 0);

		Grid barHost = new Grid { Height = 5, Margin = new Thickness(4, 0, 4, 0) };
		barHost.Children.Add(new Border
		{
			CornerRadius = new CornerRadius(2),
			Background = new SolidColorBrush(Color.FromArgb(0x44, 0x50, 0x50, 0x50))
		});
		if (safePct > 0)
		{
			Grid split = new Grid();
			split.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(safePct, GridUnitType.Star) });
			split.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(100 - safePct, GridUnitType.Star) });
			Border fill = new Border
			{
				CornerRadius = new CornerRadius(2),
				Background = new SolidColorBrush(Color.FromArgb(0xAA, 0x64, 0xC8, 0x64))
			};
			Grid.SetColumn(fill, 0);
			split.Children.Add(fill);
			barHost.Children.Add(split);
		}
		Grid.SetColumn(barHost, 1);

		TextBlock pctLabel = new TextBlock
		{
			Text = $"{safePct:F0}%",
			FontSize = 9,
			Foreground = new SolidColorBrush(Color.FromArgb(0x88, 0xDD, 0xDD, 0xDD)),
			VerticalAlignment = VerticalAlignment.Center,
			HorizontalAlignment = HorizontalAlignment.Right
		};
		Grid.SetColumn(pctLabel, 2);

		row.Children.Add(label);
		row.Children.Add(barHost);
		row.Children.Add(pctLabel);
		return row;
	}

	private void OpenTracker(string charName, string region)
	{
		CharacterPrefs.SetLastRegion(charName, region);
		Settings.Default.LastUser = charName;
		Settings.Default.LastRegion = region;
		Settings.Default.RememberMe = RememberMeCheckBox.IsChecked == true;
		Settings.Default.RememberScroll = RememberScroll.IsChecked == true;
		Settings.Default.NarratorEnabled = NarratorEnabledCheckBox.IsChecked == true;
		Settings.Default.NarratorNeural = NarratorNeuralCheckBox.IsChecked == true;
		Settings.Default.NarratorAutoRead = NarratorAutoReadCheckBox.IsChecked == true;
		Settings.Default.Save();

		TrackerLog.Info($"Open tracker: {charName} / {region}");
		new MainWindow(charName, region).Show();
		Close();
	}

	private void NewCharBtn_Click(object sender, RoutedEventArgs e)
	{
		new DialogWindow().Show();
		Close();
	}

	private void ChangeTheme_Click(object sender, RoutedEventArgs e)
	{
		new SettingsWindow().ShowDialog();
		BuildCharacterCards();
	}
}
