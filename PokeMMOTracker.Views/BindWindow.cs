using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
using System.Windows.Threading;
using PokeMMOTracker.Properties;
using PokeMMOTracker;
using SDL2;

namespace PokeMMOTracker.Views;

// Bind configuration window — visual style matches the main overlay (transparency,
// floating cards, Poppins, rounded controls).
public class BindWindow : Window
{
	private enum CaptureTarget { None, CheckKey, UncheckKey, CheckButton, UncheckButton }

	private readonly IntPtr _controller;
	private readonly FontFamily _poppins;
	private CaptureTarget _capturing = CaptureTarget.None;

	private Key _checkKey;
	private ModifierKeys _checkMods;
	private Key _uncheckKey;
	private ModifierKeys _uncheckMods;
	private int _checkButton;
	private int _uncheckButton;

	private Button _checkKeyBtn;
	private Button _uncheckKeyBtn;
	private Button _checkPadBtn;
	private Button _uncheckPadBtn;

	private DispatcherTimer _padTimer;
	private bool[] _padBaseline;

	private static readonly SolidColorBrush CardEven = new SolidColorBrush(Color.FromArgb(0x66, 0x11, 0x11, 0x11));
	private static readonly SolidColorBrush CardOdd = new SolidColorBrush(Color.FromArgb(0x66, 0x1A, 0x1A, 0x1A));
	private static readonly SolidColorBrush PanelBg = new SolidColorBrush(Color.FromArgb(0xCC, 0x1A, 0x1A, 0x1A));
	private static readonly SolidColorBrush SubtleBorder = new SolidColorBrush(Color.FromArgb(0x40, 0xFF, 0xFF, 0xFF));
	private static readonly SolidColorBrush MutedText = new SolidColorBrush(Color.FromArgb(0xB0, 0xDD, 0xDD, 0xDD));
	private static readonly SolidColorBrush AccentText = new SolidColorBrush(Color.FromArgb(0xFF, 0xA0, 0xD0, 0xFF));
	private static readonly SolidColorBrush BindIdleBorder = new SolidColorBrush(Color.FromArgb(0x55, 0xFF, 0xFF, 0xFF));
	private static readonly SolidColorBrush BindActiveBorder = new SolidColorBrush(Color.FromArgb(0xCC, 0xFF, 0xFF, 0xFF));

	public BindWindow(IntPtr controller)
	{
		_controller = controller;
		_poppins = new FontFamily(new Uri("pack://application:,,,/"), "./Fonts/#Poppins");

		Title = Loc.BindTitle;
		Width = 420;
		Height = 380;
		ResizeMode = ResizeMode.NoResize;
		WindowStartupLocation = WindowStartupLocation.CenterOwner;
		AllowsTransparency = true;
		WindowStyle = WindowStyle.None;
		Background = Brushes.Transparent;
		Foreground = Brushes.White;
		FontFamily = _poppins;
		Opacity = 0;

		LoadFromSettings();
		BuildUi();

		PreviewKeyDown += BindWindow_PreviewKeyDown;
		Loaded += delegate
		{
			var fade = new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(280))
			{
				EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
			};
			BeginAnimation(OpacityProperty, fade);
		};
		Closed += delegate { _padTimer?.Stop(); };
	}

	private void LoadFromSettings()
	{
		_checkKey = ParseKey(Settings.Default.CheckKey, Key.Down);
		_checkMods = ParseMods(Settings.Default.CheckModifiers, ModifierKeys.Control);
		_uncheckKey = ParseKey(Settings.Default.UncheckKey, Key.Up);
		_uncheckMods = ParseMods(Settings.Default.UncheckModifiers, ModifierKeys.Control);
		_checkButton = Settings.Default.CheckButton;
		_uncheckButton = Settings.Default.UncheckButton;
	}

	private static Key ParseKey(string s, Key fallback) => Enum.TryParse<Key>(s, out var k) ? k : fallback;
	private static ModifierKeys ParseMods(string s, ModifierKeys fallback) => Enum.TryParse<ModifierKeys>(s, out var m) ? m : fallback;

	private void BuildUi()
	{
		// Floating panel (same vibe as quest cards on the main overlay).
		Border shell = new Border
		{
			Background = PanelBg,
			CornerRadius = new CornerRadius(14),
			BorderBrush = SubtleBorder,
			BorderThickness = new Thickness(1),
			Margin = new Thickness(8),
			Effect = new DropShadowEffect
			{
				Color = Colors.Black,
				BlurRadius = 24,
				ShadowDepth = 0,
				Opacity = 0.45
			}
		};

		StackPanel root = new StackPanel { Margin = new Thickness(16, 14, 16, 16) };

		// Draggable header.
		Grid header = new Grid { Margin = new Thickness(0, 0, 0, 10) };
		header.MouseLeftButtonDown += delegate (object s, MouseButtonEventArgs e)
		{
			try { DragMove(); } catch { }
		};
		TextBlock title = new TextBlock
		{
			Text = Loc.BindTitle,
			FontSize = 17,
			FontWeight = FontWeights.Bold,
			HorizontalAlignment = HorizontalAlignment.Center
		};
		Button closeBtn = MakeIconButton("✕", 26, delegate { Close(); });
		closeBtn.HorizontalAlignment = HorizontalAlignment.Right;
		closeBtn.VerticalAlignment = VerticalAlignment.Top;
		closeBtn.Margin = new Thickness(0, -4, -4, 0);
		header.Children.Add(title);
		header.Children.Add(closeBtn);
		root.Children.Add(header);

		root.Children.Add(new TextBlock
		{
			Text = Loc.BindHint,
			FontSize = 11,
			Foreground = MutedText,
			TextWrapping = TextWrapping.Wrap,
			TextAlignment = TextAlignment.Center,
			Margin = new Thickness(4, 0, 4, 14)
		});

		root.Children.Add(new Border
		{
			Height = 1,
			Background = SubtleBorder,
			Margin = new Thickness(0, 0, 0, 12)
		});

		_checkKeyBtn = MakeBindButton(KeyboardDisplay(_checkKey, _checkMods), delegate { StartCapture(CaptureTarget.CheckKey); });
		_checkPadBtn = MakeBindButton(ControllerDisplay(_checkButton), delegate { StartCapture(CaptureTarget.CheckButton); });
		root.Children.Add(MakeSectionCard(Loc.BindValidateSection, CardEven,
			BindRow(Loc.Keyboard, _checkKeyBtn, Loc.Controller, _checkPadBtn)));

		_uncheckKeyBtn = MakeBindButton(KeyboardDisplay(_uncheckKey, _uncheckMods), delegate { StartCapture(CaptureTarget.UncheckKey); });
		_uncheckPadBtn = MakeBindButton(ControllerDisplay(_uncheckButton), delegate { StartCapture(CaptureTarget.UncheckButton); });
		root.Children.Add(MakeSectionCard(Loc.BindUndoSection, CardOdd,
			BindRow(Loc.Keyboard, _uncheckKeyBtn, Loc.Controller, _uncheckPadBtn)));

		StackPanel actions = new StackPanel
		{
			Orientation = Orientation.Horizontal,
			HorizontalAlignment = HorizontalAlignment.Center,
			Margin = new Thickness(0, 16, 0, 0)
		};
		Button resetBtn = MakeActionButton(Loc.Reset, false);
		resetBtn.Click += delegate { ResetDefaults(); };
		Button saveBtn = MakeActionButton(Loc.Save, true);
		saveBtn.Click += delegate { SaveAndClose(); };
		Button cancelBtn = MakeActionButton(Loc.Close, false);
		cancelBtn.Click += delegate { Close(); };
		actions.Children.Add(resetBtn);
		actions.Children.Add(saveBtn);
		actions.Children.Add(cancelBtn);
		root.Children.Add(actions);

		shell.Child = root;
		Content = shell;
	}

	private Border MakeSectionCard(string title, Brush bg, UIElement content)
	{
		StackPanel panel = new StackPanel();
		panel.Children.Add(new TextBlock
		{
			Text = title,
			FontSize = 12,
			FontWeight = FontWeights.SemiBold,
			Foreground = AccentText,
			Margin = new Thickness(0, 0, 0, 8)
		});
		panel.Children.Add(content);

		return new Border
		{
			Background = bg,
			CornerRadius = new CornerRadius(8),
			Padding = new Thickness(12, 10, 12, 10),
			Margin = new Thickness(0, 0, 0, 8),
			BorderBrush = SubtleBorder,
			BorderThickness = new Thickness(1),
			Child = panel
		};
	}

	private Grid BindRow(string leftLabel, Button leftBtn, string rightLabel, Button rightBtn)
	{
		Grid g = new Grid();
		g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(54) });
		g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
		g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(54) });
		g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

		TextBlock l1 = new TextBlock
		{
			Text = leftLabel,
			VerticalAlignment = VerticalAlignment.Center,
			Foreground = MutedText,
			FontSize = 11
		};
		Grid.SetColumn(l1, 0);
		Grid.SetColumn(leftBtn, 1);
		TextBlock l2 = new TextBlock
		{
			Text = rightLabel,
			VerticalAlignment = VerticalAlignment.Center,
			Foreground = MutedText,
			FontSize = 11,
			Margin = new Thickness(8, 0, 0, 0)
		};
		Grid.SetColumn(l2, 2);
		Grid.SetColumn(rightBtn, 3);

		g.Children.Add(l1);
		g.Children.Add(leftBtn);
		g.Children.Add(l2);
		g.Children.Add(rightBtn);
		return g;
	}

	private Button MakeBindButton(string text, RoutedEventHandler onClick)
	{
		Button b = new Button
		{
			Content = text,
			Height = 34,
			Margin = new Thickness(3, 0, 3, 0),
			Foreground = Brushes.White,
			Background = new SolidColorBrush(Color.FromArgb(0x33, 0xFF, 0xFF, 0xFF)),
			BorderBrush = BindIdleBorder,
			BorderThickness = new Thickness(1),
			Cursor = Cursors.Hand,
			FontSize = 11,
			FontFamily = _poppins,
			Template = MakeRoundedButtonTemplate(8)
		};
		b.Click += onClick;
		return b;
	}

	private Button MakeActionButton(string text, bool primary)
	{
		Brush bg = primary
			? new SolidColorBrush(Color.FromArgb(0xCC, 0x3D, 0xA9, 0x3D))
			: new SolidColorBrush(Color.FromArgb(0x99, 0x33, 0x33, 0x33));
		Brush border = primary
			? new SolidColorBrush(Color.FromArgb(0x80, 0x6F, 0xE3, 0x6F))
			: BindIdleBorder;

		return new Button
		{
			Content = text,
			Height = 34,
			MinWidth = 100,
			Margin = new Thickness(5, 0, 5, 0),
			Foreground = Brushes.White,
			Background = bg,
			BorderBrush = border,
			BorderThickness = new Thickness(1),
			Cursor = Cursors.Hand,
			FontWeight = FontWeights.SemiBold,
			FontSize = 11,
			FontFamily = _poppins,
			Template = MakeRoundedButtonTemplate(20)
		};
	}

	private Button MakeIconButton(string text, double size, RoutedEventHandler onClick)
	{
		Button b = new Button
		{
			Content = text,
			Width = size,
			Height = size,
			Padding = new Thickness(0),
			Foreground = MutedText,
			Background = Brushes.Transparent,
			BorderThickness = new Thickness(0),
			Cursor = Cursors.Hand,
			FontSize = 12,
			Template = MakeRoundedButtonTemplate(6)
		};
		b.Click += onClick;
		return b;
	}

	private static ControlTemplate MakeRoundedButtonTemplate(double cornerRadius)
	{
		ControlTemplate template = new ControlTemplate(typeof(Button));
		FrameworkElementFactory border = new FrameworkElementFactory(typeof(Border));
		border.Name = "border";
		border.SetValue(Border.CornerRadiusProperty, new CornerRadius(cornerRadius));
		border.SetBinding(Border.BackgroundProperty, new System.Windows.Data.Binding("Background") { RelativeSource = new System.Windows.Data.RelativeSource(System.Windows.Data.RelativeSourceMode.TemplatedParent) });
		border.SetBinding(Border.BorderBrushProperty, new System.Windows.Data.Binding("BorderBrush") { RelativeSource = new System.Windows.Data.RelativeSource(System.Windows.Data.RelativeSourceMode.TemplatedParent) });
		border.SetBinding(Border.BorderThicknessProperty, new System.Windows.Data.Binding("BorderThickness") { RelativeSource = new System.Windows.Data.RelativeSource(System.Windows.Data.RelativeSourceMode.TemplatedParent) });

		FrameworkElementFactory content = new FrameworkElementFactory(typeof(ContentPresenter));
		content.SetValue(ContentPresenter.HorizontalAlignmentProperty, HorizontalAlignment.Center);
		content.SetValue(ContentPresenter.VerticalAlignmentProperty, VerticalAlignment.Center);
		content.SetValue(ContentPresenter.MarginProperty, new Thickness(8, 2, 8, 2));
		border.AppendChild(content);
		template.VisualTree = border;
		return template;
	}

	private void SetBindButtonCapturing(Button btn, bool capturing)
	{
		btn.BorderBrush = capturing ? BindActiveBorder : BindIdleBorder;
		btn.Background = capturing
			? new SolidColorBrush(Color.FromArgb(0x55, 0xFF, 0xFF, 0xFF))
			: new SolidColorBrush(Color.FromArgb(0x33, 0xFF, 0xFF, 0xFF));
		if (capturing)
		{
			btn.Effect = new DropShadowEffect
			{
				Color = Colors.White,
				BlurRadius = 12,
				ShadowDepth = 0,
				Opacity = 0.35
			};
		}
		else
		{
			btn.Effect = null;
		}
	}

	private void StartCapture(CaptureTarget target)
	{
		CancelCapture();
		_capturing = target;

		if (target == CaptureTarget.CheckKey)
		{
			_checkKeyBtn.Content = Loc.PressKey;
			SetBindButtonCapturing(_checkKeyBtn, true);
		}
		else if (target == CaptureTarget.UncheckKey)
		{
			_uncheckKeyBtn.Content = Loc.PressKey;
			SetBindButtonCapturing(_uncheckKeyBtn, true);
		}
		else if (target == CaptureTarget.CheckButton || target == CaptureTarget.UncheckButton)
		{
			if (_controller == IntPtr.Zero)
			{
				MessageBox.Show(Loc.NoController, Loc.BindTitle, MessageBoxButton.OK, MessageBoxImage.Information);
				_capturing = CaptureTarget.None;
				return;
			}
			Button padBtn = target == CaptureTarget.CheckButton ? _checkPadBtn : _uncheckPadBtn;
			padBtn.Content = Loc.PressButton;
			SetBindButtonCapturing(padBtn, true);
			StartPadCapture();
		}
	}

	private void CancelCapture()
	{
		_padTimer?.Stop();
		_capturing = CaptureTarget.None;
		RefreshLabels();
	}

	private void RefreshLabels()
	{
		_checkKeyBtn.Content = KeyboardDisplay(_checkKey, _checkMods);
		_uncheckKeyBtn.Content = KeyboardDisplay(_uncheckKey, _uncheckMods);
		_checkPadBtn.Content = ControllerDisplay(_checkButton);
		_uncheckPadBtn.Content = ControllerDisplay(_uncheckButton);
		SetBindButtonCapturing(_checkKeyBtn, false);
		SetBindButtonCapturing(_uncheckKeyBtn, false);
		SetBindButtonCapturing(_checkPadBtn, false);
		SetBindButtonCapturing(_uncheckPadBtn, false);
	}

	private void BindWindow_PreviewKeyDown(object sender, KeyEventArgs e)
	{
		if (_capturing != CaptureTarget.CheckKey && _capturing != CaptureTarget.UncheckKey) return;

		Key key = (e.Key == Key.System) ? e.SystemKey : e.Key;

		if (key == Key.Escape)
		{
			CancelCapture();
			e.Handled = true;
			return;
		}

		if (key == Key.LeftCtrl || key == Key.RightCtrl ||
			key == Key.LeftShift || key == Key.RightShift ||
			key == Key.LeftAlt || key == Key.RightAlt ||
			key == Key.LWin || key == Key.RWin || key == Key.System)
		{
			e.Handled = true;
			return;
		}

		ModifierKeys mods = Keyboard.Modifiers;
		if (_capturing == CaptureTarget.CheckKey)
		{
			_checkKey = key;
			_checkMods = mods;
		}
		else
		{
			_uncheckKey = key;
			_uncheckMods = mods;
		}

		_capturing = CaptureTarget.None;
		RefreshLabels();
		e.Handled = true;
	}

	private void StartPadCapture()
	{
		SDL.SDL_PumpEvents();
		int max = (int)SDL.SDL_GameControllerButton.SDL_CONTROLLER_BUTTON_MAX;
		_padBaseline = new bool[max];
		for (int i = 0; i < max; i++)
		{
			_padBaseline[i] = SDL.SDL_GameControllerGetButton(_controller, (SDL.SDL_GameControllerButton)i) == 1;
		}

		if (_padTimer == null)
		{
			_padTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(30) };
			_padTimer.Tick += PadTimer_Tick;
		}
		_padTimer.Start();
	}

	private void PadTimer_Tick(object sender, EventArgs e)
	{
		try
		{
			SDL.SDL_PumpEvents();
			int max = (int)SDL.SDL_GameControllerButton.SDL_CONTROLLER_BUTTON_MAX;
			for (int i = 0; i < max; i++)
			{
				bool pressed = SDL.SDL_GameControllerGetButton(_controller, (SDL.SDL_GameControllerButton)i) == 1;
				if (pressed && !_padBaseline[i])
				{
					if (_capturing == CaptureTarget.CheckButton) _checkButton = i;
					else if (_capturing == CaptureTarget.UncheckButton) _uncheckButton = i;

					_padTimer.Stop();
					_capturing = CaptureTarget.None;
					RefreshLabels();
					return;
				}
			}
		}
		catch
		{
			_padTimer.Stop();
			_capturing = CaptureTarget.None;
			RefreshLabels();
		}
	}

	private void ResetDefaults()
	{
		_checkKey = Key.Down;
		_checkMods = ModifierKeys.Control;
		_uncheckKey = Key.Up;
		_uncheckMods = ModifierKeys.Control;
		_checkButton = (int)SDL.SDL_GameControllerButton.SDL_CONTROLLER_BUTTON_RIGHTSHOULDER;
		_uncheckButton = (int)SDL.SDL_GameControllerButton.SDL_CONTROLLER_BUTTON_LEFTSHOULDER;
		CancelCapture();
	}

	private void SaveAndClose()
	{
		Settings.Default.CheckKey = _checkKey.ToString();
		Settings.Default.CheckModifiers = _checkMods.ToString();
		Settings.Default.UncheckKey = _uncheckKey.ToString();
		Settings.Default.UncheckModifiers = _uncheckMods.ToString();
		Settings.Default.CheckButton = _checkButton;
		Settings.Default.UncheckButton = _uncheckButton;
		Settings.Default.Save();
		Close();
	}

	private static string KeyboardDisplay(Key key, ModifierKeys mods)
	{
		string s = "";
		if (mods.HasFlag(ModifierKeys.Control)) s += "Ctrl + ";
		if (mods.HasFlag(ModifierKeys.Shift)) s += "Shift + ";
		if (mods.HasFlag(ModifierKeys.Alt)) s += "Alt + ";
		if (mods.HasFlag(ModifierKeys.Windows)) s += "Win + ";
		return s + key.ToString();
	}

	private static string ControllerDisplay(int button)
	{
		switch ((SDL.SDL_GameControllerButton)button)
		{
			case SDL.SDL_GameControllerButton.SDL_CONTROLLER_BUTTON_A: return "A";
			case SDL.SDL_GameControllerButton.SDL_CONTROLLER_BUTTON_B: return "B";
			case SDL.SDL_GameControllerButton.SDL_CONTROLLER_BUTTON_X: return "X";
			case SDL.SDL_GameControllerButton.SDL_CONTROLLER_BUTTON_Y: return "Y";
			case SDL.SDL_GameControllerButton.SDL_CONTROLLER_BUTTON_BACK: return "Back / Select";
			case SDL.SDL_GameControllerButton.SDL_CONTROLLER_BUTTON_GUIDE: return "Guide";
			case SDL.SDL_GameControllerButton.SDL_CONTROLLER_BUTTON_START: return "Start";
			case SDL.SDL_GameControllerButton.SDL_CONTROLLER_BUTTON_LEFTSTICK: return "L3";
			case SDL.SDL_GameControllerButton.SDL_CONTROLLER_BUTTON_RIGHTSTICK: return "R3";
			case SDL.SDL_GameControllerButton.SDL_CONTROLLER_BUTTON_LEFTSHOULDER: return "L1 / LB";
			case SDL.SDL_GameControllerButton.SDL_CONTROLLER_BUTTON_RIGHTSHOULDER: return "R1 / RB";
			case SDL.SDL_GameControllerButton.SDL_CONTROLLER_BUTTON_DPAD_UP: return Loc.DPadUp;
			case SDL.SDL_GameControllerButton.SDL_CONTROLLER_BUTTON_DPAD_DOWN: return Loc.DPadDown;
			case SDL.SDL_GameControllerButton.SDL_CONTROLLER_BUTTON_DPAD_LEFT: return Loc.DPadLeft;
			case SDL.SDL_GameControllerButton.SDL_CONTROLLER_BUTTON_DPAD_RIGHT: return Loc.DPadRight;
			default: return Loc.ButtonN + button;
		}
	}
}
