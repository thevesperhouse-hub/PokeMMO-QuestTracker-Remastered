using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
using System.Windows.Threading;
using SDL2;

namespace PokeMMOTracker.Views;

/// <summary>
/// On-screen A-Z / 0-9 keyboard with gamepad navigation (same SDL edge + event model as hub).
/// </summary>
public sealed class GamepadKeyboardControl : UserControl
{
	private static readonly string[] Rows = { "ABCDEFGHIJ", "KLMNOPQRST", "UVWXYZ0123", "456789⌫OK" };

	private readonly TextBlock _display;
	private readonly List<List<Button>> _keys = new();
	private readonly FontFamily _font;
	private DispatcherTimer? _timer;
	private IntPtr _controller = IntPtr.Zero;
	private bool _captureInput = false;
	private bool _prevUp, _prevDown, _prevLeft, _prevRight, _prevA;
	private int _row = 0;
	private int _col = 0;
	private Button? _highlighted;
	private string _text = "";

	public int MaxLength { get; set; } = 16;

	public string Text
	{
		get => _text;
		set
		{
			_text = value ?? "";
			if (_text.Length > MaxLength)
				_text = _text.Substring(0, MaxLength);
			UpdateDisplay();
		}
	}

	public bool CaptureInput
	{
		get => _captureInput;
		set
		{
			_captureInput = value;
			if (value)
			{
				ResetPrevStates();
				UpdateHighlight();
			}
			else
				ClearHighlight();
		}
	}

	public event Action<string>? TextChanged;
	public event Action? Confirm;
	public event Action? RequestLeaveDown;

	public GamepadKeyboardControl()
	{
		_font = new FontFamily(new Uri("pack://application:,,,/"), "./Fonts/#Poppins");
		Background = Brushes.Transparent;

		StackPanel root = new StackPanel { Margin = new Thickness(4, 0, 4, 0) };

		_display = new TextBlock
		{
			FontFamily = _font,
			FontSize = 18,
			Foreground = Brushes.White,
			Background = new SolidColorBrush(Color.FromRgb(0x2A, 0x2A, 0x2A)),
			Padding = new Thickness(10, 8, 10, 8),
			Margin = new Thickness(0, 0, 0, 8),
			Text = "_"
		};
		root.Children.Add(_display);

		Grid grid = new Grid { Margin = new Thickness(0, 0, 0, 4) };
		for (int r = 0; r < Rows.Length; r++)
		{
			_keys.Add(new List<Button>());
			StackPanel rowPanel = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Center };
			foreach (char c in Rows[r])
			{
				string label = c.ToString();
				Button key = MakeKey(label);
				_keys[r].Add(key);
				rowPanel.Children.Add(key);
			}
			root.Children.Add(rowPanel);
		}

		Content = root;
		Loaded += OnLoaded;
		Unloaded += OnUnloaded;
	}

	private Button MakeKey(string label)
	{
		Button btn = new Button
		{
			Content = label,
			Width = label == "OK" ? 58 : 32,
			Height = 36,
			Margin = new Thickness(2),
			FontFamily = _font,
			FontSize = label.Length > 1 ? 11 : 13,
			Foreground = Brushes.White,
			Cursor = Cursors.Hand,
			Focusable = false,
			IsTabStop = false,
			BorderThickness = new Thickness(1),
			Background = label == "OK"
				? new SolidColorBrush(Color.FromArgb(0x99, 0x3D, 0x6B, 0x9E))
				: new SolidColorBrush(Color.FromArgb(0x88, 0x28, 0x28, 0x28)),
			BorderBrush = new SolidColorBrush(Color.FromArgb(0x88, 0x55, 0x55, 0x55))
		};
		btn.Resources.Add(typeof(Border), new Style(typeof(Border))
		{
			Setters = { new Setter(Border.CornerRadiusProperty, new CornerRadius(5)) }
		});
		btn.Click += (_, __) => PressKey(label);
		return btn;
	}

	private void OnLoaded(object sender, RoutedEventArgs e)
	{
		try
		{
			SDL.SDL_Init(SDL.SDL_INIT_GAMECONTROLLER);
			_timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(32) };
			_timer.Tick += OnTick;
			_timer.Start();
		}
		catch { }
		UpdateHighlight();
	}

	private void OnUnloaded(object sender, RoutedEventArgs e)
	{
		if (_timer != null)
		{
			_timer.Stop();
			_timer = null;
		}
		if (_controller != IntPtr.Zero)
		{
			try { SDL.SDL_GameControllerClose(_controller); } catch { }
			_controller = IntPtr.Zero;
		}
	}

	private void OnTick(object? sender, EventArgs e)
	{
		if (!_captureInput) return;

		try
		{
			SDL.SDL_PumpEvents();
			EnsureController();
			if (_controller == IntPtr.Zero ||
			    SDL.SDL_GameControllerGetAttached(_controller) != SDL.SDL_bool.SDL_TRUE)
				return;

			bool ateUp = false, ateDown = false, ateLeft = false, ateRight = false, ateA = false;
			ProcessDpadEvents(ref ateUp, ref ateDown, ref ateLeft, ref ateRight, ref ateA);

			bool up = Btn(SDL.SDL_GameControllerButton.SDL_CONTROLLER_BUTTON_DPAD_UP);
			bool down = Btn(SDL.SDL_GameControllerButton.SDL_CONTROLLER_BUTTON_DPAD_DOWN);
			bool left = Btn(SDL.SDL_GameControllerButton.SDL_CONTROLLER_BUTTON_DPAD_LEFT);
			bool right = Btn(SDL.SDL_GameControllerButton.SDL_CONTROLLER_BUTTON_DPAD_RIGHT);
			bool a = Btn(SDL.SDL_GameControllerButton.SDL_CONTROLLER_BUTTON_A);

			if (up && !_prevUp && !ateUp && _row > 0)
			{
				_row--;
				_col = Math.Min(_col, _keys[_row].Count - 1);
				UpdateHighlight();
			}
			if (down && !_prevDown && !ateDown)
			{
				if (_row < _keys.Count - 1)
				{
					_row++;
					_col = Math.Min(_col, _keys[_row].Count - 1);
					UpdateHighlight();
				}
				else
					RequestLeaveDown?.Invoke();
			}
			if (left && !_prevLeft && !ateLeft && _col > 0)
			{
				_col--;
				UpdateHighlight();
			}
			if (right && !_prevRight && !ateRight && _col < _keys[_row].Count - 1)
			{
				_col++;
				UpdateHighlight();
			}

			if (a && !_prevA && !ateA)
				PressSelectedKey();

			_prevUp = up;
			_prevDown = down;
			_prevLeft = left;
			_prevRight = right;
			_prevA = a;
		}
		catch { }
	}

	private void ProcessDpadEvents(ref bool ateUp, ref bool ateDown, ref bool ateLeft, ref bool ateRight, ref bool ateA)
	{
		SDL.SDL_Event[] events = new SDL.SDL_Event[1];
		while (SDL.SDL_PeepEvents(events, 1, SDL.SDL_eventaction.SDL_GETEVENT,
			       SDL.SDL_EventType.SDL_CONTROLLERBUTTONDOWN,
			       SDL.SDL_EventType.SDL_CONTROLLERBUTTONDOWN) > 0)
		{
			switch ((SDL.SDL_GameControllerButton)events[0].cbutton.button)
			{
				case SDL.SDL_GameControllerButton.SDL_CONTROLLER_BUTTON_DPAD_UP:
					ateUp = true;
					if (_row > 0)
					{
						_row--;
						_col = Math.Min(_col, _keys[_row].Count - 1);
						UpdateHighlight();
					}
					break;
				case SDL.SDL_GameControllerButton.SDL_CONTROLLER_BUTTON_DPAD_DOWN:
					ateDown = true;
					if (_row < _keys.Count - 1)
					{
						_row++;
						_col = Math.Min(_col, _keys[_row].Count - 1);
						UpdateHighlight();
					}
					else
						RequestLeaveDown?.Invoke();
					break;
				case SDL.SDL_GameControllerButton.SDL_CONTROLLER_BUTTON_DPAD_LEFT:
					ateLeft = true;
					if (_col > 0)
					{
						_col--;
						UpdateHighlight();
					}
					break;
				case SDL.SDL_GameControllerButton.SDL_CONTROLLER_BUTTON_DPAD_RIGHT:
					ateRight = true;
					if (_col < _keys[_row].Count - 1)
					{
						_col++;
						UpdateHighlight();
					}
					break;
				case SDL.SDL_GameControllerButton.SDL_CONTROLLER_BUTTON_A:
					ateA = true;
					_prevA = true;
					PressSelectedKey();
					break;
			}
		}
	}

	private void PressSelectedKey()
	{
		if (_row < 0 || _row >= _keys.Count) return;
		if (_col < 0 || _col >= _keys[_row].Count) return;
		string label = (string)_keys[_row][_col].Content;
		PressKey(label);
	}

	private void PressKey(string label)
	{
		if (label == "⌫")
		{
			if (_text.Length > 0)
			{
				_text = _text.Substring(0, _text.Length - 1);
				UpdateDisplay();
				TextChanged?.Invoke(_text);
			}
			return;
		}
		if (label == "OK")
		{
			Confirm?.Invoke();
			return;
		}
		if (_text.Length >= MaxLength) return;
		_text += label;
		UpdateDisplay();
		TextChanged?.Invoke(_text);
	}

	private void UpdateDisplay()
	{
		_display.Text = string.IsNullOrEmpty(_text) ? "_" : _text;
	}

	private void UpdateHighlight()
	{
		ClearHighlight();
		if (_row < 0 || _row >= _keys.Count) return;
		if (_col < 0 || _col >= _keys[_row].Count) return;

		Button btn = _keys[_row][_col];
		btn.BorderThickness = new Thickness(2);
		btn.BorderBrush = Brushes.White;
		btn.Background = new SolidColorBrush(Color.FromArgb(0xAA, 0x3D, 0x6B, 0x9E));
		ApplyGlow(btn);
		_highlighted = btn;
	}

	private void ClearHighlight()
	{
		if (_highlighted == null) return;
		_highlighted.Effect = null;
		string label = (string)_highlighted.Content;
		_highlighted.BorderThickness = new Thickness(1);
		_highlighted.BorderBrush = new SolidColorBrush(Color.FromArgb(0x88, 0x55, 0x55, 0x55));
		_highlighted.Background = label == "OK"
			? new SolidColorBrush(Color.FromArgb(0x99, 0x3D, 0x6B, 0x9E))
			: new SolidColorBrush(Color.FromArgb(0x88, 0x28, 0x28, 0x28));
		_highlighted = null;
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

	private void ResetPrevStates()
	{
		_prevUp = false;
		_prevDown = false;
		_prevLeft = false;
		_prevRight = false;
		_prevA = false;
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
}
