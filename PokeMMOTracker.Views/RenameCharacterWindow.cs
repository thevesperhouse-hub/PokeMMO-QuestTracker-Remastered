using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
using System.Windows.Threading;
using SDL2;
using PokeMMOTracker;
using PokeMMOTracker.Properties;

namespace PokeMMOTracker.Views;

public partial class RenameCharacterWindow : Window
{
	private enum RenameZone { Keyboard, Save, Cancel }

	private readonly string _oldName;
	private DispatcherTimer? _padTimer;
	private IntPtr _controller = IntPtr.Zero;
	private bool _padSuspended;
	private bool _prevUp, _prevDown, _prevA;
	private RenameZone _zone = RenameZone.Keyboard;
	private FrameworkElement? _lastHighlight;
	private GamepadKeyboardControl _nameKeyboard;

	public string? NewName { get; private set; }

	public RenameCharacterWindow(string currentName)
	{
		_oldName = currentName;
		InitializeComponent();
		_nameKeyboard = new GamepadKeyboardControl();
		KeyboardHost.Children.Add(_nameKeyboard);
		CharacterNameTB.Text = currentName;
		ApplyLocalization();
		WireKeyboard();
		Loaded += OnLoaded;
		Closed += delegate { StopPad(); };
	}

	private void WireKeyboard()
	{
		_nameKeyboard.Text = _oldName;
		_nameKeyboard.TextChanged += text => CharacterNameTB.Text = text;
		_nameKeyboard.RequestLeaveDown += () => EnterZone(RenameZone.Save);
		_nameKeyboard.Confirm += () => EnterZone(RenameZone.Save);
	}

	private void OnLoaded(object sender, RoutedEventArgs e)
	{
		try
		{
			SDL.SDL_Init(SDL.SDL_INIT_GAMECONTROLLER);
			_padTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(32) };
			_padTimer.Tick += PadTick;
			_padTimer.Start();
		}
		catch (Exception ex)
		{
			TrackerLog.Error("Rename SDL init: " + ex);
		}
		EnterZone(RenameZone.Keyboard);
	}

	private void EnterZone(RenameZone zone)
	{
		_zone = zone;
		if (zone == RenameZone.Keyboard)
		{
			_nameKeyboard.CaptureInput = true;
			_padSuspended = true;
		}
		else
		{
			_nameKeyboard.CaptureInput = false;
			_padSuspended = false;
			_prevUp = false;
			_prevDown = false;
			_prevA = false;
		}
		UpdateHighlight();
	}

	private void ApplyLocalization()
	{
		Title = Loc.RenameCharacterTitle;
		TitleText.Text = Loc.RenameCharacterTitle;
		NameLabel.Text = Loc.CharacterNameLabel;
		KeyboardHintText.Text = Loc.KeyboardHint;
		SaveBtn.Content = Loc.Save;
		CancelBtn.Content = Loc.Close;
	}

	private void SaveBtn_Click(object sender, RoutedEventArgs e)
	{
		string name = CharacterNameTB.Text.Trim();
		if (!DatabaseHelper.IsValidCharacterName(name))
		{
			MessageBox.Show(Loc.ErrNameInvalid, Loc.RenameCharacterTitle, MessageBoxButton.OK, MessageBoxImage.Warning);
			return;
		}
		if (name != _oldName && DatabaseHelper.CharacterExists(DatabaseHelper.GetDatabasePath(), name))
		{
			MessageBox.Show(Loc.ErrNameTaken, Loc.RenameCharacterTitle, MessageBoxButton.OK, MessageBoxImage.Warning);
			return;
		}

		string dbPath = DatabaseHelper.GetDatabasePath();
		if (!DatabaseHelper.RenameCharacter(dbPath, _oldName, name))
		{
			MessageBox.Show(Loc.ErrRenameFailed, Loc.RenameCharacterTitle, MessageBoxButton.OK, MessageBoxImage.Error);
			return;
		}

		CharacterPrefs.RenameCharacter(_oldName, name);
		if (Settings.Default.LastUser == _oldName)
			Settings.Default.LastUser = name;
		Settings.Default.Save();

		NewName = name;
		DialogResult = true;
		Close();
	}

	private void CancelBtn_Click(object sender, RoutedEventArgs e)
	{
		DialogResult = false;
		Close();
	}

	private void StopPad()
	{
		if (_padTimer != null)
		{
			_padTimer.Stop();
			_padTimer = null;
		}
		if (_controller != IntPtr.Zero)
		{
			try { SDL.SDL_GameControllerClose(_controller); } catch { }
			_controller = IntPtr.Zero;
		}
		_nameKeyboard.CaptureInput = false;
	}

	private void PadTick(object? sender, EventArgs e)
	{
		if (_padSuspended || _zone == RenameZone.Keyboard) return;

		try
		{
			SDL.SDL_PumpEvents();
			EnsureController();
			if (_controller == IntPtr.Zero ||
			    SDL.SDL_GameControllerGetAttached(_controller) != SDL.SDL_bool.SDL_TRUE)
				return;

			bool up = Btn(SDL.SDL_GameControllerButton.SDL_CONTROLLER_BUTTON_DPAD_UP);
			bool down = Btn(SDL.SDL_GameControllerButton.SDL_CONTROLLER_BUTTON_DPAD_DOWN);
			bool a = Btn(SDL.SDL_GameControllerButton.SDL_CONTROLLER_BUTTON_A);

			if (down && !_prevDown)
			{
				if (_zone == RenameZone.Save)
					_zone = RenameZone.Cancel;
				UpdateHighlight();
			}
			if (up && !_prevUp)
			{
				if (_zone == RenameZone.Cancel)
					_zone = RenameZone.Save;
				else if (_zone == RenameZone.Save)
					EnterZone(RenameZone.Keyboard);
				UpdateHighlight();
			}

			if (a && !_prevA)
			{
				if (_zone == RenameZone.Save)
					SaveBtn_Click(SaveBtn, new RoutedEventArgs());
				else if (_zone == RenameZone.Cancel)
					CancelBtn_Click(CancelBtn, new RoutedEventArgs());
			}

			_prevUp = up;
			_prevDown = down;
			_prevA = a;
		}
		catch { }
	}

	private bool Btn(SDL.SDL_GameControllerButton button) =>
		SDL.SDL_GameControllerGetButton(_controller, button) == 1;

	private void EnsureController()
	{
		if (_controller != IntPtr.Zero) return;
		for (int i = 0; i < SDL.SDL_NumJoysticks(); i++)
		{
			if (SDL.SDL_IsGameController(i) == SDL.SDL_bool.SDL_TRUE)
			{
				_controller = SDL.SDL_GameControllerOpen(i);
				break;
			}
		}
	}

	private void UpdateHighlight()
	{
		if (_zone == RenameZone.Keyboard) return;

		if (_lastHighlight is Button oldBtn)
		{
			oldBtn.Effect = null;
			oldBtn.BorderThickness = new Thickness(1);
			if (oldBtn == SaveBtn)
			{
				oldBtn.BorderBrush = new SolidColorBrush(Color.FromArgb(0xCC, 0x6A, 0x9F, 0xD4));
				oldBtn.Background = new SolidColorBrush(Color.FromArgb(0xFF, 0x3D, 0x6B, 0x9E));
			}
			else
			{
				oldBtn.BorderBrush = new SolidColorBrush(Color.FromArgb(0x44, 0x44, 0x44, 0x44));
				oldBtn.Background = new SolidColorBrush(Color.FromArgb(0x33, 0x1A, 0x1A, 0x1A));
			}
		}

		Button btn = _zone == RenameZone.Save ? SaveBtn : CancelBtn;
		btn.BorderThickness = new Thickness(2);
		btn.BorderBrush = Brushes.White;
		if (btn != SaveBtn)
			btn.Background = new SolidColorBrush(Color.FromArgb(0x88, 0x3D, 0x6B, 0x9E));
		ApplyGlow(btn);
		_lastHighlight = btn;
	}

	private static void ApplyGlow(UIElement el)
	{
		var glow = new DropShadowEffect { Color = Colors.White, ShadowDepth = 0, BlurRadius = 10, Opacity = 0.4 };
		el.Effect = glow;
		var ease = new SineEase { EasingMode = EasingMode.EaseInOut };
		glow.BeginAnimation(DropShadowEffect.BlurRadiusProperty,
			new DoubleAnimation(8, 18, TimeSpan.FromMilliseconds(900)) { AutoReverse = true, RepeatBehavior = RepeatBehavior.Forever, EasingFunction = ease });
		glow.BeginAnimation(DropShadowEffect.OpacityProperty,
			new DoubleAnimation(0.35, 0.65, TimeSpan.FromMilliseconds(900)) { AutoReverse = true, RepeatBehavior = RepeatBehavior.Forever, EasingFunction = ease });
	}
}
