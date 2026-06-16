using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Effects;
using PokeMMOTracker;

namespace PokeMMOTracker.Views;

// Grid picker — cropped previews only, no labels.
public class AvatarPickerWindow : Window
{
	private readonly FontFamily _poppins;
	private readonly string _currentId;
	private ControllerGroupNav? _padNav;
	private readonly List<Button> _avatarButtons = new();
	private Button? _closeBtn;

	public string SelectedAvatarId { get; private set; }

	public AvatarPickerWindow(string currentAvatarId, Window? owner = null)
	{
		_currentId = AvatarCatalog.ResolveId(currentAvatarId);
		SelectedAvatarId = _currentId;
		_poppins = new FontFamily(new Uri("pack://application:,,,/"), "./Fonts/#Poppins");
		if (owner != null) Owner = owner;

		Title = Loc.AvatarPickerTitle;
		Width = 380;
		Height = 320;
		ResizeMode = ResizeMode.NoResize;
		WindowStartupLocation = owner != null ? WindowStartupLocation.CenterOwner : WindowStartupLocation.CenterScreen;
		AllowsTransparency = true;
		WindowStyle = WindowStyle.None;
		Background = Brushes.Transparent;
		Foreground = Brushes.White;
		FontFamily = _poppins;

		BuildUi();
		Loaded += OnLoaded;
		Closed += delegate { _padNav?.Dispose(); };
	}

	private void OnLoaded(object sender, RoutedEventArgs e)
	{
		_padNav = new ControllerGroupNav();
		_padNav.BackAction = () =>
		{
			DialogResult = false;
			Close();
		};

		if (_avatarButtons.Count > 0)
		{
			var gridItems = new List<(FrameworkElement, Action)>();
			foreach (Button btn in _avatarButtons)
				gridItems.Add((btn, () => btn.RaiseEvent(new RoutedEventArgs(Button.ClickEvent))));
			_padNav.AddGrid(4, gridItems);
		}

		if (_closeBtn != null)
			_padNav.AddButton(_closeBtn);

		int startIdx = Array.FindIndex(AvatarCatalog.All, e => e.Id == _currentId);
		if (startIdx < 0) startIdx = 0;
		_padNav.FocusAt(0, startIdx);
	}

	private void BuildUi()
	{
		Border shell = new Border
		{
			Background = new SolidColorBrush(Color.FromArgb(0xEE, 0x14, 0x14, 0x14)),
			BorderBrush = new SolidColorBrush(Color.FromArgb(0x55, 0xFF, 0xFF, 0xFF)),
			BorderThickness = new Thickness(1),
			CornerRadius = new CornerRadius(14),
			Padding = new Thickness(16),
			Effect = new DropShadowEffect
			{
				Color = Colors.Black,
				BlurRadius = 24,
				ShadowDepth = 0,
				Opacity = 0.6
			}
		};

		StackPanel root = new StackPanel();

		TextBlock title = new TextBlock
		{
			Text = Loc.AvatarPickerTitle,
			FontSize = 15,
			FontWeight = FontWeights.SemiBold,
			Foreground = Brushes.White,
			Margin = new Thickness(0, 0, 0, 12),
			HorizontalAlignment = HorizontalAlignment.Center
		};
		root.Children.Add(title);

		UniformGrid grid = new UniformGrid
		{
			Columns = 4,
			Rows = 2,
			HorizontalAlignment = HorizontalAlignment.Center
		};

		foreach (AvatarCatalog.Entry entry in AvatarCatalog.All)
		{
			bool selected = entry.Id == _currentId;
			Button btn = new Button
			{
				Width = 72,
				Height = 72,
				Padding = new Thickness(4),
				Margin = new Thickness(4),
				Tag = entry.Id,
				Cursor = Cursors.Hand,
				BorderThickness = new Thickness(selected ? 2 : 1),
				BorderBrush = selected
					? new SolidColorBrush(Color.FromArgb(0xCC, 0x6A, 0x9F, 0xD4))
					: new SolidColorBrush(Color.FromArgb(0x40, 0xFF, 0xFF, 0xFF)),
				Background = selected
					? new SolidColorBrush(Color.FromArgb(0x55, 0x3D, 0x6B, 0x9E))
					: new SolidColorBrush(Color.FromArgb(0x66, 0x22, 0x22, 0x22)),
				Content = new Image
				{
					Source = AppAssets.GetAvatarCropped(entry.Id),
					Stretch = Stretch.UniformToFill,
					Width = 60,
					Height = 60
				}
			};
			btn.Resources.Add(typeof(Border), new Style(typeof(Border))
			{
				Setters = { new Setter(Border.CornerRadiusProperty, new CornerRadius(8)) }
			});
			btn.Click += AvatarButton_Click;
			grid.Children.Add(btn);
			_avatarButtons.Add(btn);
		}

		root.Children.Add(grid);

		_closeBtn = new Button
		{
			Content = Loc.Close,
			Margin = new Thickness(0, 14, 0, 0),
			Padding = new Thickness(12, 8, 12, 8),
			HorizontalAlignment = HorizontalAlignment.Center,
			Foreground = new SolidColorBrush(Color.FromArgb(0xCC, 0xDD, 0xDD, 0xDD)),
			Background = new SolidColorBrush(Color.FromArgb(0x44, 0x33, 0x33, 0x33)),
			BorderBrush = new SolidColorBrush(Color.FromArgb(0x55, 0x88, 0x88, 0x88)),
			BorderThickness = new Thickness(1),
			Cursor = Cursors.Hand
		};
		_closeBtn.Resources.Add(typeof(Border), new Style(typeof(Border))
		{
			Setters = { new Setter(Border.CornerRadiusProperty, new CornerRadius(8)) }
		});
		_closeBtn.Click += delegate
		{
			DialogResult = false;
			Close();
		};
		root.Children.Add(_closeBtn);

		shell.Child = root;
		Content = shell;
	}

	private void AvatarButton_Click(object sender, RoutedEventArgs e)
	{
		if (sender is Button btn && btn.Tag is string id)
		{
			SelectedAvatarId = id;
			DialogResult = true;
			Close();
		}
	}
}
