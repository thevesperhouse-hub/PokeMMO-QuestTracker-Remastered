using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using SDL2;
using PokeMMOTracker;
using PokeMMOTracker.Models;
using PokeMMOTracker.Properties;

namespace PokeMMOTracker.Views;

public partial class DialogWindow : Window
{
	private enum DialogZone { Keyboard, Avatar, Regions, Continue, Back }

	private static readonly string[] Regions = { "Kanto", "Johto", "Hoenn", "Sinnoh", "Unova" };
	private string _selectedRegion = "";
	private string _selectedAvatar = AvatarCatalog.DefaultId;
	private readonly FontFamily _poppins = new FontFamily(new Uri("pack://application:,,,/"), "./Fonts/#Poppins");
	private readonly List<Button> _regionChipButtons = new();
	private DispatcherTimer? _dialogPadTimer;
	private IntPtr _dialogController = IntPtr.Zero;
	private bool _dialogPadSuspended;
	private bool _prevUp, _prevDown, _prevLeft, _prevRight, _prevA;
	private DialogZone _zone = DialogZone.Keyboard;
	private int _regionIndex = 0;
	private FrameworkElement? _lastHighlight;
	private GamepadKeyboardControl _nameKeyboard;
	private bool _ignoreClickThrough;

	public DialogWindow()
	{
		InitializeComponent();
		_nameKeyboard = new GamepadKeyboardControl();
		KeyboardHost.Children.Add(_nameKeyboard);
		Icon = BitmapFrame.Create((BitmapSource)AppAssets.AppIcon);
		ApplyLocalization();
		UpdateAvatarPreview();
		BuildRegionChips();
		WireKeyboard();
		Loaded += OnLoaded;
		Closed += delegate { StopDialogPad(); };
	}

	private void WireKeyboard()
	{
		_nameKeyboard.TextChanged += text =>
		{
			CharacterNameTB.Text = text;
		};
		_nameKeyboard.RequestLeaveDown += () => EnterZone(DialogZone.Avatar);
		_nameKeyboard.Confirm += () => EnterZone(DialogZone.Avatar);
	}

	private void OnLoaded(object sender, RoutedEventArgs e)
	{
		try
		{
			SDL.SDL_Init(SDL.SDL_INIT_GAMECONTROLLER);
			_dialogPadTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(32) };
			_dialogPadTimer.Tick += DialogPadTick;
			_dialogPadTimer.Start();
		}
		catch (Exception ex)
		{
			TrackerLog.Error("Dialog SDL init: " + ex);
		}

		EnterZone(DialogZone.Keyboard);
	}

	private void EnterZone(DialogZone zone)
	{
		_zone = zone;
		if (zone == DialogZone.Keyboard)
		{
			_nameKeyboard.CaptureInput = true;
			_dialogPadSuspended = true;
		}
		else
		{
			_nameKeyboard.CaptureInput = false;
			_dialogPadSuspended = false;
			ResetDialogPadPrev();
		}
		UpdateDialogHighlight();
	}

	private void ApplyLocalization()
	{
		Title = Loc.DialogWindowTitle;
		TitleText.Text = Loc.CreateCharacterTitle;
		NameLabel.Text = Loc.CharacterNameLabel;
		KeyboardHintText.Text = Loc.KeyboardHint;
		AvatarLabel.Text = Loc.AvatarLabel;
		AvatarPickBtn.ToolTip = Loc.AvatarPickToolTip;
		RegionLabel.Text = Loc.StartingRegionLabel;
		ContinueBtn.Content = Loc.Continue;
		BackBtn.Content = Loc.Back;
	}

	private void UpdateAvatarPreview()
	{
		AvatarPreviewImage.Source = AppAssets.GetAvatarCropped(_selectedAvatar);
	}

	private void AvatarPickBtn_Click(object sender, RoutedEventArgs e)
	{
		SuspendDialogPad();
		var picker = new AvatarPickerWindow(_selectedAvatar, this);
		picker.ShowDialog();
		if (picker.DialogResult == true)
		{
			_selectedAvatar = picker.SelectedAvatarId;
			UpdateAvatarPreview();
		}
		SuppressClickThrough();
		ResumeDialogPad();
	}

	private void SuppressClickThrough()
	{
		// After ShowDialog(), mouse-up can land on Continue/Back under the cursor (classic WPF click-through).
		_ignoreClickThrough = true;
		if (Mouse.LeftButton == MouseButtonState.Pressed)
			Mouse.Capture(this);
		Dispatcher.BeginInvoke(DispatcherPriority.Input, () =>
		{
			if (Mouse.Captured == this)
				Mouse.Capture(null);
			_ignoreClickThrough = false;
		});
	}

	private void BuildRegionChips()
	{
		RegionChips.Children.Clear();
		_regionChipButtons.Clear();

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
				Focusable = false,
				IsTabStop = false,
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
			_regionChipButtons.Add(chip);
		}

		_regionIndex = Math.Clamp(_regionIndex, 0, Math.Max(0, _regionChipButtons.Count - 1));
	}

	private void RegionChip_Click(object sender, RoutedEventArgs e)
	{
		if (sender is Button btn && btn.Tag is string region)
		{
			_selectedRegion = region;
			BuildRegionChips();
			UpdateDialogHighlight();
		}
	}

	private void Button_Click(object sender, RoutedEventArgs e)
	{
		if (_ignoreClickThrough) return;
		if (string.IsNullOrEmpty(_selectedRegion))
		{
			MessageBox.Show(Loc.ErrSelectRegion, Loc.DialogWindowTitle, MessageBoxButton.OK, MessageBoxImage.Warning);
			return;
		}
		if (!DatabaseHelper.IsValidCharacterName(CharacterNameTB.Text.Trim()))
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
		CharacterPrefs.SetAvatar(name, _selectedAvatar);

		new MainWindow(name, _selectedRegion).Show();
		Close();
	}

	private void BackBtn_Click(object sender, RoutedEventArgs e)
	{
		if (_ignoreClickThrough) return;
		new LoginWindow().Show();
		Close();
	}

	private void SuspendDialogPad()
	{
		_dialogPadSuspended = true;
		_nameKeyboard.CaptureInput = false;
		ClearDialogHighlight();
	}

	private void ResumeDialogPad()
	{
		_dialogPadSuspended = false;
		EnterZone(_zone);
	}

	private void StopDialogPad()
	{
		if (_dialogPadTimer != null)
		{
			_dialogPadTimer.Stop();
			_dialogPadTimer = null;
		}
		if (_dialogController != IntPtr.Zero)
		{
			try { SDL.SDL_GameControllerClose(_dialogController); } catch { }
			_dialogController = IntPtr.Zero;
		}
		_nameKeyboard.CaptureInput = false;
	}

	private void DialogPadTick(object? sender, EventArgs e)
	{
		if (_dialogPadSuspended || _zone == DialogZone.Keyboard) return;

		try
		{
			SDL.SDL_PumpEvents();
			EnsureDialogController();
			if (_dialogController == IntPtr.Zero ||
			    SDL.SDL_GameControllerGetAttached(_dialogController) != SDL.SDL_bool.SDL_TRUE)
				return;

			bool ateUp = false, ateDown = false, ateLeft = false, ateRight = false;
			ProcessDialogDpadEvents(ref ateUp, ref ateDown, ref ateLeft, ref ateRight);

			bool up = Btn(SDL.SDL_GameControllerButton.SDL_CONTROLLER_BUTTON_DPAD_UP);
			bool down = Btn(SDL.SDL_GameControllerButton.SDL_CONTROLLER_BUTTON_DPAD_DOWN);
			bool left = Btn(SDL.SDL_GameControllerButton.SDL_CONTROLLER_BUTTON_DPAD_LEFT);
			bool right = Btn(SDL.SDL_GameControllerButton.SDL_CONTROLLER_BUTTON_DPAD_RIGHT);
			bool a = Btn(SDL.SDL_GameControllerButton.SDL_CONTROLLER_BUTTON_A);

			if (down && !_prevDown && !ateDown)
			{
				DialogNavDown();
				UpdateDialogHighlight();
			}
			if (up && !_prevUp && !ateUp)
			{
				DialogNavUp();
				UpdateDialogHighlight();
			}

			if (_zone == DialogZone.Regions && _regionChipButtons.Count > 0)
			{
				if (left && !_prevLeft && !ateLeft && _regionIndex > 0)
				{
					_regionIndex--;
					UpdateDialogHighlight();
				}
				if (right && !_prevRight && !ateRight && _regionIndex < _regionChipButtons.Count - 1)
				{
					_regionIndex++;
					UpdateDialogHighlight();
				}
			}

			if (a && !_prevA)
				DialogActivate();

			_prevUp = up;
			_prevDown = down;
			_prevLeft = left;
			_prevRight = right;
			_prevA = a;
		}
		catch { }
	}

	private void ProcessDialogDpadEvents(ref bool ateUp, ref bool ateDown, ref bool ateLeft, ref bool ateRight)
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
					DialogNavUp();
					UpdateDialogHighlight();
					break;
				case SDL.SDL_GameControllerButton.SDL_CONTROLLER_BUTTON_DPAD_DOWN:
					ateDown = true;
					DialogNavDown();
					UpdateDialogHighlight();
					break;
				case SDL.SDL_GameControllerButton.SDL_CONTROLLER_BUTTON_DPAD_LEFT:
					ateLeft = true;
					if (_zone == DialogZone.Regions && _regionIndex > 0)
					{
						_regionIndex--;
						UpdateDialogHighlight();
					}
					break;
				case SDL.SDL_GameControllerButton.SDL_CONTROLLER_BUTTON_DPAD_RIGHT:
					ateRight = true;
					if (_zone == DialogZone.Regions && _regionIndex < _regionChipButtons.Count - 1)
					{
						_regionIndex++;
						UpdateDialogHighlight();
					}
					break;
				case SDL.SDL_GameControllerButton.SDL_CONTROLLER_BUTTON_A:
					DialogActivate();
					break;
			}
		}
	}

	private void DialogNavDown()
	{
		switch (_zone)
		{
			case DialogZone.Avatar:
				_zone = DialogZone.Regions;
				break;
			case DialogZone.Regions:
				_zone = DialogZone.Continue;
				break;
			case DialogZone.Continue:
				_zone = DialogZone.Back;
				break;
		}
	}

	private void DialogNavUp()
	{
		switch (_zone)
		{
			case DialogZone.Back:
				_zone = DialogZone.Continue;
				break;
			case DialogZone.Continue:
				_zone = DialogZone.Regions;
				break;
			case DialogZone.Regions:
				_zone = DialogZone.Avatar;
				break;
			case DialogZone.Avatar:
				EnterZone(DialogZone.Keyboard);
				break;
		}
	}

	private void DialogActivate()
	{
		switch (_zone)
		{
			case DialogZone.Avatar:
				AvatarPickBtn_Click(AvatarPickBtn, new RoutedEventArgs());
				break;
			case DialogZone.Regions:
				if (_regionIndex >= 0 && _regionIndex < _regionChipButtons.Count)
					_regionChipButtons[_regionIndex].RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
				break;
			case DialogZone.Continue:
				Button_Click(ContinueBtn, new RoutedEventArgs());
				break;
			case DialogZone.Back:
				BackBtn_Click(BackBtn, new RoutedEventArgs());
				break;
		}
	}

	private void ResetDialogPadPrev()
	{
		_prevUp = false;
		_prevDown = false;
		_prevLeft = false;
		_prevRight = false;
		_prevA = false;
	}

	private bool Btn(SDL.SDL_GameControllerButton button) =>
		SDL.SDL_GameControllerGetButton(_dialogController, button) == 1;

	private void EnsureDialogController()
	{
		if (_dialogController != IntPtr.Zero) return;
		for (int i = 0; i < SDL.SDL_NumJoysticks(); i++)
		{
			if (SDL.SDL_IsGameController(i) == SDL.SDL_bool.SDL_TRUE)
			{
				_dialogController = SDL.SDL_GameControllerOpen(i);
				break;
			}
		}
	}

	private void ClearDialogHighlight()
	{
		if (_lastHighlight != null)
		{
			_lastHighlight.Effect = null;
			if (_lastHighlight is Button btn)
			{
				btn.BorderThickness = new Thickness(1);
				if (_lastHighlight == AvatarPickBtn)
				{
					btn.BorderBrush = new SolidColorBrush(Color.FromArgb(0x55, 0xFF, 0xFF, 0xFF));
					btn.Background = new SolidColorBrush(Color.FromArgb(0x33, 0x1A, 0x1A, 0x1A));
				}
				else if (_lastHighlight == ContinueBtn)
				{
					btn.BorderBrush = new SolidColorBrush(Color.FromArgb(0xCC, 0x6A, 0x9F, 0xD4));
					btn.Background = new SolidColorBrush(Color.FromArgb(0xFF, 0x3D, 0x6B, 0x9E));
				}
				else if (_lastHighlight == BackBtn)
				{
					btn.BorderBrush = new SolidColorBrush(Color.FromArgb(0x44, 0x44, 0x44, 0x44));
					btn.Background = new SolidColorBrush(Color.FromArgb(0x33, 0x1A, 0x1A, 0x1A));
				}
				else if (_regionChipButtons.Contains(btn))
					BuildRegionChips();
			}
			_lastHighlight = null;
		}
	}

	private void UpdateDialogHighlight()
	{
		if (_zone == DialogZone.Keyboard) return;

		ClearDialogHighlight();
		FrameworkElement? target = null;

		switch (_zone)
		{
			case DialogZone.Avatar:
				target = AvatarPickBtn;
				break;
			case DialogZone.Regions when _regionChipButtons.Count > 0:
				target = _regionChipButtons[_regionIndex];
				break;
			case DialogZone.Continue:
				target = ContinueBtn;
				break;
			case DialogZone.Back:
				target = BackBtn;
				break;
		}

		if (target is Button btn)
		{
			btn.BorderThickness = new Thickness(2);
			btn.BorderBrush = Brushes.White;
			if (btn != ContinueBtn)
				btn.Background = new SolidColorBrush(Color.FromArgb(0x88, 0x3D, 0x6B, 0x9E));
			ApplyDialogGlow(btn);
			_lastHighlight = btn;
		}
	}

	private static void ApplyDialogGlow(UIElement el)
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
