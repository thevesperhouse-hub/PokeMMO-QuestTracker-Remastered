using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
using SDL2;
using PokeMMOTracker;
using PokeMMOTracker.Models;
using PokeMMOTracker.Properties;

namespace PokeMMOTracker.Views;

// Character hub — PureRef-style cards, shared DB progress per character.
public partial class LoginWindow : Window
{
	private static readonly string[] Regions = { "Kanto", "Johto", "Hoenn", "Sinnoh", "Unova" };
	private readonly string _dbPath = DatabaseHelper.GetDatabasePath();
	private enum HubZone { Lang, Card, Footer }

	private readonly FontFamily _poppins = new FontFamily(new Uri("pack://application:,,,/"), "./Fonts/#Poppins");
	private DispatcherTimer? _hubPadTimer;
	private IntPtr _hubController = IntPtr.Zero;
	private bool _hubPadSuspended;
	private bool _prevUp, _prevDown, _prevLeft, _prevRight, _prevA;
	private bool _hubActionArmed;
	private HubZone _hubZone = HubZone.Lang;
	private int _langIndex = 0;
	private int _cardIndex = 0;
	private int _chipIndex = 0;
	private int _footerRow = 0;
	private int _footerCol = 0;
	private FrameworkElement? _lastHubHighlight;
	private readonly List<List<FrameworkElement>> _cardNavGroups = new();
	private readonly List<Border> _cardBorders = new();

	private const int FooterRowCount = 4;

	private sealed class ChipVisualState
	{
		public Brush BorderBrush;
		public Brush Background;
		public Thickness BorderThickness;

		public ChipVisualState(Button btn)
		{
			BorderBrush = btn.BorderBrush;
			Background = btn.Background;
			BorderThickness = btn.BorderThickness;
		}

		public void Apply(Button btn)
		{
			btn.BorderBrush = BorderBrush;
			btn.Background = Background;
			btn.BorderThickness = BorderThickness;
			btn.Effect = null;
		}
	}

	private static void RememberChipStyle(Button btn) => btn.Tag = new ChipVisualState(btn);

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
		Closed += delegate { StopHubPad(); };
	}

	private readonly record struct HubNavSnapshot(HubZone Zone, int Lang, int Card, int Chip, int FooterRow, int FooterCol);

	private HubNavSnapshot CaptureHubNav() =>
		new(_hubZone, _langIndex, _cardIndex, _chipIndex, _footerRow, _footerCol);

	private void RestoreHubNav(HubNavSnapshot snap)
	{
		_hubZone = snap.Zone;
		_langIndex = snap.Lang;
		_cardIndex = snap.Card;
		_chipIndex = snap.Chip;
		_footerRow = snap.FooterRow;
		_footerCol = snap.FooterCol;
		ClampHubNavIndices();
	}

	private int FooterColCount(int row) => row switch { 0 => 2, 1 => 2, 2 => 1, 3 => 2, _ => 1 };

	private void ClampHubNavIndices()
	{
		_langIndex = Math.Clamp(_langIndex, 0, 1);
		if (_cardNavGroups.Count == 0 && _hubZone == HubZone.Card)
			_hubZone = HubZone.Lang;
		if (_cardNavGroups.Count > 0)
		{
			_cardIndex = Math.Clamp(_cardIndex, 0, _cardNavGroups.Count - 1);
			_chipIndex = Math.Clamp(_chipIndex, 0, _cardNavGroups[_cardIndex].Count - 1);
		}
		_footerRow = Math.Clamp(_footerRow, 0, FooterRowCount - 1);
		_footerCol = Math.Clamp(_footerCol, 0, FooterColCount(_footerRow) - 1);
	}

	private void StopHubPad()
	{
		if (_hubPadTimer != null)
		{
			_hubPadTimer.Stop();
			_hubPadTimer = null;
		}
		if (_hubController != IntPtr.Zero)
		{
			try { SDL.SDL_GameControllerClose(_hubController); } catch { }
			_hubController = IntPtr.Zero;
		}
	}

	private void ResetHubPadPrevStates()
	{
		_hubActionArmed = false;
		_prevUp = false;
		_prevDown = false;
		_prevLeft = false;
		_prevRight = false;
		_prevA = false;
	}

	private void SuspendPadNav()
	{
		_hubPadSuspended = true;
		ClearHubHighlight();
	}

	private void ResumePadNav(HubNavSnapshot? snap = null)
	{
		_hubPadSuspended = false;
		if (snap.HasValue)
			RestoreHubNav(snap.Value);
		else
		{
			_hubZone = HubZone.Lang;
			_langIndex = 0;
			ClampHubNavIndices();
		}
		ResetHubPadPrevStates();
		UpdateHubSelectionVisuals();
	}

	private void HubPadTimer_Tick(object? sender, EventArgs e)
	{
		if (_hubPadSuspended) return;

		try
		{
			SDL.SDL_PumpEvents();
			EnsureHubController();
			if (_hubController == IntPtr.Zero ||
			    SDL.SDL_GameControllerGetAttached(_hubController) != SDL.SDL_bool.SDL_TRUE)
				return;

			bool ateUp = false, ateDown = false, ateLeft = false, ateRight = false;
			ProcessHubDpadEvents(ref ateUp, ref ateDown, ref ateLeft, ref ateRight);

			bool upPressed = HubBtn(SDL.SDL_GameControllerButton.SDL_CONTROLLER_BUTTON_DPAD_UP);
			bool downPressed = HubBtn(SDL.SDL_GameControllerButton.SDL_CONTROLLER_BUTTON_DPAD_DOWN);
			bool leftPressed = HubBtn(SDL.SDL_GameControllerButton.SDL_CONTROLLER_BUTTON_DPAD_LEFT);
			bool rightPressed = HubBtn(SDL.SDL_GameControllerButton.SDL_CONTROLLER_BUTTON_DPAD_RIGHT);
			bool aPressed = HubBtn(SDL.SDL_GameControllerButton.SDL_CONTROLLER_BUTTON_A);

			if (!_hubActionArmed && !aPressed)
				_hubActionArmed = true;

			bool horizHeld = leftPressed || rightPressed;

			if (downPressed && !_prevDown && !ateDown && !(_hubZone == HubZone.Card && horizHeld))
			{
				HubNavDown();
				UpdateHubSelectionVisuals();
			}

			if (upPressed && !_prevUp && !ateUp && !(_hubZone == HubZone.Card && horizHeld))
			{
				HubNavUp();
				UpdateHubSelectionVisuals();
			}

			if (_hubZone == HubZone.Lang)
			{
				if (leftPressed && !_prevLeft && !ateLeft && _langIndex > 0)
				{
					_langIndex--;
					UpdateHubSelectionVisuals();
				}
				if (rightPressed && !_prevRight && !ateRight && _langIndex < 1)
				{
					_langIndex++;
					UpdateHubSelectionVisuals();
				}
			}
			else if (_hubZone == HubZone.Card && _cardNavGroups.Count > 0)
			{
				int maxChip = _cardNavGroups[_cardIndex].Count - 1;
				if (leftPressed && !_prevLeft && !ateLeft && _chipIndex > 0)
				{
					_chipIndex--;
					UpdateHubSelectionVisuals();
				}
				if (rightPressed && !_prevRight && !ateRight && _chipIndex < maxChip)
				{
					_chipIndex++;
					UpdateHubSelectionVisuals();
				}
			}
			else if (_hubZone == HubZone.Footer)
			{
				int maxCol = FooterColCount(_footerRow) - 1;
				if (leftPressed && !_prevLeft && !ateLeft && _footerCol > 0)
				{
					_footerCol--;
					UpdateHubSelectionVisuals();
				}
				if (rightPressed && !_prevRight && !ateRight && _footerCol < maxCol)
				{
					_footerCol++;
					UpdateHubSelectionVisuals();
				}
			}

			if (aPressed && !_prevA && _hubActionArmed)
				HubActivateCurrent();

			_prevUp = upPressed;
			_prevDown = downPressed;
			_prevLeft = leftPressed;
			_prevRight = rightPressed;
			_prevA = aPressed;
		}
		catch { }
	}

	private void ProcessHubDpadEvents(ref bool ateUp, ref bool ateDown, ref bool ateLeft, ref bool ateRight)
	{
		SDL.SDL_Event[] events = new SDL.SDL_Event[1];
		while (SDL.SDL_PeepEvents(events, 1, SDL.SDL_eventaction.SDL_GETEVENT,
			       SDL.SDL_EventType.SDL_CONTROLLERBUTTONDOWN,
			       SDL.SDL_EventType.SDL_CONTROLLERBUTTONDOWN) > 0)
		{
			if (events[0].type != SDL.SDL_EventType.SDL_CONTROLLERBUTTONDOWN)
				continue;

			switch ((SDL.SDL_GameControllerButton)events[0].cbutton.button)
			{
				case SDL.SDL_GameControllerButton.SDL_CONTROLLER_BUTTON_DPAD_UP:
					ateUp = true;
					HubNavUp();
					UpdateHubSelectionVisuals();
					break;
				case SDL.SDL_GameControllerButton.SDL_CONTROLLER_BUTTON_DPAD_DOWN:
					ateDown = true;
					HubNavDown();
					UpdateHubSelectionVisuals();
					break;
				case SDL.SDL_GameControllerButton.SDL_CONTROLLER_BUTTON_DPAD_LEFT:
					ateLeft = true;
					HubNavLeft();
					UpdateHubSelectionVisuals();
					break;
				case SDL.SDL_GameControllerButton.SDL_CONTROLLER_BUTTON_DPAD_RIGHT:
					ateRight = true;
					HubNavRight();
					UpdateHubSelectionVisuals();
					break;
			}
		}
	}

	private void HubNavLeft()
	{
		switch (_hubZone)
		{
			case HubZone.Lang:
				if (_langIndex > 0) _langIndex--;
				break;
			case HubZone.Card:
				if (_cardNavGroups.Count > 0 && _chipIndex > 0) _chipIndex--;
				break;
			case HubZone.Footer:
				if (_footerCol > 0) _footerCol--;
				break;
		}
	}

	private void HubNavRight()
	{
		switch (_hubZone)
		{
			case HubZone.Lang:
				if (_langIndex < 1) _langIndex++;
				break;
			case HubZone.Card:
				if (_cardNavGroups.Count > 0 && _chipIndex < _cardNavGroups[_cardIndex].Count - 1)
					_chipIndex++;
				break;
			case HubZone.Footer:
				if (_footerCol < FooterColCount(_footerRow) - 1) _footerCol++;
				break;
		}
	}

	private void HubNavDown()
	{
		switch (_hubZone)
		{
			case HubZone.Lang:
				if (_cardNavGroups.Count > 0)
				{
					_hubZone = HubZone.Card;
					_cardIndex = 0;
					_chipIndex = 0;
					CenterCardInScroller(_cardBorders[_cardIndex]);
				}
				else
				{
					_hubZone = HubZone.Footer;
					_footerRow = 0;
					_footerCol = 0;
				}
				break;
			case HubZone.Card:
				if (_cardIndex < _cardNavGroups.Count - 1)
				{
					_cardIndex++;
					_chipIndex = 0;
					CenterCardInScroller(_cardBorders[_cardIndex]);
				}
				else
				{
					_hubZone = HubZone.Footer;
					_footerRow = 0;
					_footerCol = 0;
				}
				break;
			case HubZone.Footer:
				if (_footerRow < FooterRowCount - 1)
				{
					_footerRow++;
					_footerCol = 0;
				}
				break;
		}
	}

	private void HubNavUp()
	{
		switch (_hubZone)
		{
			case HubZone.Footer:
				if (_footerRow > 0)
				{
					_footerRow--;
					_footerCol = 0;
				}
				else if (_cardNavGroups.Count > 0)
				{
					_hubZone = HubZone.Card;
					_cardIndex = _cardNavGroups.Count - 1;
					_chipIndex = 0;
					CenterCardInScroller(_cardBorders[_cardIndex]);
				}
				else
				{
					_hubZone = HubZone.Lang;
					_langIndex = 0;
				}
				break;
			case HubZone.Card:
				if (_cardIndex > 0)
				{
					_cardIndex--;
					_chipIndex = 0;
					CenterCardInScroller(_cardBorders[_cardIndex]);
				}
				else
				{
					_hubZone = HubZone.Lang;
					_langIndex = 0;
				}
				break;
			case HubZone.Lang:
				break;
		}
	}

	private FrameworkElement GetFooterElement(int row, int col)
	{
		switch (row)
		{
			case 0: return col == 0 ? RememberMeCheckBox : RememberScroll;
			case 1: return col == 0 ? NarratorEnabledCheckBox : NarratorNeuralCheckBox;
			case 2: return NarratorAutoReadCheckBox;
			case 3: return col == 0 ? NewCharBtn : ChangeTheme;
			default: return RememberMeCheckBox;
		}
	}

	private void HubActivateCurrent()
	{
		switch (_hubZone)
		{
			case HubZone.Lang:
				if (_langIndex == 0)
					LangEnBtn_Click(LangEnBtn, new RoutedEventArgs());
				else
					LangFrBtn_Click(LangFrBtn, new RoutedEventArgs());
				break;
			case HubZone.Card:
				if (_cardNavGroups.Count > 0)
					ActivateNavElement(_cardNavGroups[_cardIndex][_chipIndex]);
				break;
			case HubZone.Footer:
				FrameworkElement el = GetFooterElement(_footerRow, _footerCol);
				if (el is Button btn)
					btn.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
				else if (el is CheckBox box)
				{
					box.IsChecked = !box.IsChecked;
					if (box == NarratorEnabledCheckBox)
						UpdateNarratorOptionStates();
				}
				break;
		}
	}

	private bool HubBtn(SDL.SDL_GameControllerButton button)
	{
		return SDL.SDL_GameControllerGetButton(_hubController, button) == 1;
	}

	private void EnsureHubController()
	{
		if (_hubController != IntPtr.Zero) return;
		for (int i = 0; i < SDL.SDL_NumJoysticks(); i++)
		{
			if (SDL.SDL_IsGameController(i) == SDL.SDL_bool.SDL_TRUE)
			{
				_hubController = SDL.SDL_GameControllerOpen(i);
				break;
			}
		}
	}

	private void ClearHubHighlight()
	{
		if (_lastHubHighlight != null)
		{
			if (_lastHubHighlight is Button btn && btn.Tag is ChipVisualState chipStyle)
				chipStyle.Apply(btn);
			else
			{
				_lastHubHighlight.Effect = null;
				if (_lastHubHighlight is Button langOrFooterBtn)
				{
					langOrFooterBtn.BorderThickness = new Thickness(1);
					if (_lastHubHighlight == LangEnBtn || _lastHubHighlight == LangFrBtn)
						UpdateLangButtons();
					else if (_lastHubHighlight == NewCharBtn || _lastHubHighlight == ChangeTheme)
					{
						langOrFooterBtn.BorderBrush = new SolidColorBrush(Color.FromArgb(0x55, 0xFF, 0xFF, 0xFF));
						langOrFooterBtn.Background = new SolidColorBrush(Color.FromArgb(0x66, 0x1A, 0x1A, 0x1A));
					}
				}
			}
			_lastHubHighlight = null;
		}

		ResetAllChipStyles();
		ResetCardBorders();
		NavHighlightRing.Visibility = Visibility.Collapsed;
	}

	private void ResetAllChipStyles()
	{
		foreach (List<FrameworkElement> row in _cardNavGroups)
		{
			foreach (FrameworkElement el in row)
			{
				if (el is Button btn && btn.Tag is ChipVisualState cs)
					cs.Apply(btn);
			}
		}
	}

	private void ResetCardBorders()
	{
		foreach (Border card in _cardBorders)
		{
			bool lastUsed = (string?)card.Tag == Settings.Default.LastUser;
			card.BorderBrush = lastUsed
				? new SolidColorBrush(Color.FromArgb(0xCC, 0xA0, 0xD0, 0xFF))
				: new SolidColorBrush(Color.FromArgb(0x40, 0xFF, 0xFF, 0xFF));
			card.BorderThickness = lastUsed ? new Thickness(2) : new Thickness(1);
			card.Effect = null;
		}
	}

	private void UpdateHubSelectionVisuals()
	{
		ClearHubHighlight();
		UpdateLangButtons();

		switch (_hubZone)
		{
			case HubZone.Lang:
				Button langBtn = _langIndex == 0 ? LangEnBtn : LangFrBtn;
				langBtn.BorderThickness = new Thickness(2);
				langBtn.BorderBrush = Brushes.White;
				ApplyHubGlow(langBtn);
				_lastHubHighlight = langBtn;
				break;
			case HubZone.Card when _cardNavGroups.Count > 0:
				Border card = _cardBorders[_cardIndex];
				card.BorderThickness = new Thickness(2);
				card.BorderBrush = new SolidColorBrush(Color.FromArgb(0xCC, 0xA0, 0xD0, 0xFF));
				FrameworkElement chipEl = _cardNavGroups[_cardIndex][_chipIndex];
				if (chipEl is Button chipBtn)
				{
					chipBtn.BorderThickness = new Thickness(2);
					chipBtn.BorderBrush = Brushes.White;
					chipBtn.Background = new SolidColorBrush(Color.FromArgb(0xAA, 0x3D, 0x6B, 0x9E));
					ApplyHubGlow(chipBtn);
					_lastHubHighlight = chipBtn;
				}
				break;
			case HubZone.Footer:
				FrameworkElement footerEl = GetFooterElement(_footerRow, _footerCol);
				if (footerEl is Button footerBtn)
				{
					footerBtn.BorderThickness = new Thickness(2);
					footerBtn.BorderBrush = Brushes.White;
					footerBtn.Background = new SolidColorBrush(Color.FromArgb(0x88, 0x3D, 0x6B, 0x9E));
				}
				ApplyHubGlow(footerEl);
				_lastHubHighlight = footerEl;
				break;
		}
	}

	private void ApplyHubGlow(UIElement el)
	{
		var glow = new DropShadowEffect { Color = Colors.White, ShadowDepth = 0, BlurRadius = 10, Opacity = 0.4 };
		el.Effect = glow;
		var ease = new SineEase { EasingMode = EasingMode.EaseInOut };
		glow.BeginAnimation(DropShadowEffect.BlurRadiusProperty,
			new DoubleAnimation(8, 20, TimeSpan.FromMilliseconds(950))
			{
				AutoReverse = true,
				RepeatBehavior = RepeatBehavior.Forever,
				EasingFunction = ease
			});
		glow.BeginAnimation(DropShadowEffect.OpacityProperty,
			new DoubleAnimation(0.35, 0.7, TimeSpan.FromMilliseconds(950))
			{
				AutoReverse = true,
				RepeatBehavior = RepeatBehavior.Forever,
				EasingFunction = ease
			});
	}

	private void ActivateNavElement(FrameworkElement el)
	{
		if (el is Button btn)
			btn.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
	}

	private void CenterCardInScroller(FrameworkElement card)
	{
		if (card.ActualHeight <= 0)
		{
			card.Dispatcher.BeginInvoke(new Action(() => CenterCardInScroller(card)), System.Windows.Threading.DispatcherPriority.Loaded);
			return;
		}

		GeneralTransform transform = card.TransformToAncestor(CardsScroller);
		Point pos = transform.Transform(new Point(0, 0));
		double cardTop = pos.Y;
		double cardHeight = card.ActualHeight;
		double viewport = CardsScroller.ViewportHeight;
		double targetOffset = CardsScroller.VerticalOffset + cardTop - (viewport - cardHeight) / 2;
		CardsScroller.ScrollToVerticalOffset(Math.Max(0, targetOffset));
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
		KeyboardNavigation.SetTabNavigation(CardsScroller, KeyboardNavigationMode.None);
		KeyboardNavigation.SetDirectionalNavigation(CardsScroller, KeyboardNavigationMode.None);
		KeyboardNavigation.SetTabNavigation(CardsPanel, KeyboardNavigationMode.None);

		try
		{
			SDL.SDL_Init(SDL.SDL_INIT_GAMECONTROLLER);
			_hubPadTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(32) };
			_hubPadTimer.Tick += HubPadTimer_Tick;
			_hubPadTimer.Start();
		}
		catch (Exception ex)
		{
			TrackerLog.Error("Hub SDL init: " + ex);
		}

		try
		{
			BuildCharacterCards();
		}
		catch (Exception ex)
		{
			TrackerLog.Error("Hub BuildCharacterCards: " + ex);
			ShowHubError(Loc.Pick("Could not load character cards.", "Impossible de charger les personnages."));
		}

		ResetHubPadPrevStates();
		if (_cardNavGroups.Count > 0)
		{
			_hubZone = HubZone.Card;
			_cardIndex = 0;
			string last = Settings.Default.LastUser;
			if (!string.IsNullOrEmpty(last))
			{
				for (int i = 0; i < _cardBorders.Count; i++)
				{
					if ((string?)_cardBorders[i].Tag == last)
					{
						_cardIndex = i;
						break;
					}
				}
			}
			_chipIndex = 0;
			CenterCardInScroller(_cardBorders[_cardIndex]);
		}
		else
		{
			_hubZone = HubZone.Lang;
			_langIndex = 0;
		}
		UpdateHubSelectionVisuals();
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
		_cardNavGroups.Clear();
		_cardBorders.Clear();
		List<string> names = SortCharactersForHub(DatabaseHelper.GetAllCharacterNames(_dbPath));

		if (names.Count == 0)
		{
			CardsPanel.Children.Add(new TextBlock
			{
				Text = Loc.Pick("No characters yet — create one!", "Aucun personnage — crée-en un !"),
				Foreground = new SolidColorBrush(Color.FromArgb(0x99, 0xDD, 0xDD, 0xDD)),
				Margin = new Thickness(20),
				FontSize = 13
			});
			ClampHubNavIndices();
			UpdateHubSelectionVisuals();
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

		ClampHubNavIndices();
		UpdateHubSelectionVisuals();
	}

	private static List<string> SortCharactersForHub(List<string> names)
	{
		string lastUser = Settings.Default.LastUser;
		if (string.IsNullOrEmpty(lastUser) || !names.Contains(lastUser))
			return names;

		return new[] { lastUser }.Concat(names.Where(n => n != lastUser)).ToList();
	}

	private void OpenRenameDialog(string currentName)
	{
		var snap = CaptureHubNav();
		SuspendPadNav();
		var dlg = new RenameCharacterWindow(currentName) { Owner = this };
		if (dlg.ShowDialog() == true)
			BuildCharacterCards();
		ResumePadNav(snap);
	}

	private Border BuildErrorCard(string name)
	{
		return new Border
		{
			Width = HubCardWidth,
			Margin = new Thickness(0, 0, 0, 16),
			Padding = new Thickness(16),
			CornerRadius = new CornerRadius(14),
			Background = new SolidColorBrush(Color.FromArgb(0x44, 0x40, 0x10, 0x10)),
			BorderBrush = new SolidColorBrush(Color.FromArgb(0x55, 0xFF, 0x66, 0x66)),
			BorderThickness = new Thickness(1),
			Child = new TextBlock
			{
				Text = Loc.Pick($"Could not load {name}", $"Impossible de charger {name}"),
				Foreground = new SolidColorBrush(Color.FromArgb(0xCC, 0xFF, 0xAA, 0xAA)),
				TextWrapping = TextWrapping.Wrap,
				FontFamily = _poppins
			}
		};
	}

	private const double HubCardWidth = 820;
	private const double HubAvatarColumnWidth = 240;

	private Border BuildCharacterCard(CharacterDashboardInfo info, bool highlightLastUsed = false)
	{
		string avatarId = CharacterPrefs.GetAvatar(info.Name);

		Border card = new Border
		{
			Width = HubCardWidth,
			MinHeight = 300,
			Margin = new Thickness(0, 0, 0, 16),
			CornerRadius = new CornerRadius(14),
			Background = new SolidColorBrush(Color.FromArgb(0x99, 0x14, 0x14, 0x14)),
			BorderBrush = highlightLastUsed
				? new SolidColorBrush(Color.FromArgb(0xCC, 0xA0, 0xD0, 0xFF))
				: new SolidColorBrush(Color.FromArgb(0x40, 0xFF, 0xFF, 0xFF)),
			BorderThickness = highlightLastUsed ? new Thickness(2) : new Thickness(1),
			Cursor = Cursors.Hand,
			ClipToBounds = true,
			Padding = new Thickness(16, 14, 16, 14),
			Tag = info.Name
		};
		List<FrameworkElement> navItems = new();

		Grid body = new Grid();
		body.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
		body.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(HubAvatarColumnWidth) });

		StackPanel left = new StackPanel { Margin = new Thickness(0, 0, 12, 0) };

		Grid nameRow = new Grid();
		nameRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

		TextBlock nameText = new TextBlock
		{
			Text = info.Name,
			FontSize = 24,
			FontWeight = FontWeights.Bold,
			Foreground = Brushes.White,
			FontFamily = _poppins,
			VerticalAlignment = VerticalAlignment.Center
		};
		Grid.SetColumn(nameText, 0);
		nameRow.Children.Add(nameText);

		left.Children.Add(nameRow);

		Button renameBtn = new Button
		{
			Content = "✎",
			FontSize = 14,
			Padding = new Thickness(8, 4, 8, 4),
			Margin = new Thickness(0, 0, 6, 0),
			Cursor = Cursors.Hand,
			FontFamily = _poppins,
			Foreground = new SolidColorBrush(Color.FromArgb(0xCC, 0xDD, 0xDD, 0xDD)),
			Background = new SolidColorBrush(Color.FromArgb(0x44, 0x33, 0x33, 0x33)),
			BorderBrush = new SolidColorBrush(Color.FromArgb(0x55, 0x88, 0x88, 0x88)),
			BorderThickness = new Thickness(1),
			Focusable = true,
			IsTabStop = false,
			ToolTip = Loc.RenameCharacterToolTip,
			VerticalAlignment = VerticalAlignment.Center
		};
		renameBtn.Resources.Add(typeof(Border), new Style(typeof(Border))
		{
			Setters = { new Setter(Border.CornerRadiusProperty, new CornerRadius(6)) }
		});
		renameBtn.Click += delegate
		{
			OpenRenameDialog(info.Name);
		};
		RememberChipStyle(renameBtn);

		left.Children.Add(new TextBlock
		{
			Text = Loc.RefCardGlobal($"{SafePercent(info.GlobalPercent):F1}"),
			FontSize = 11,
			Foreground = new SolidColorBrush(Color.FromArgb(0xFF, 0xA0, 0xD0, 0xFF)),
			Margin = new Thickness(0, 4, 0, 6),
			FontFamily = _poppins
		});

		Grid globalBar = CreateHorizontalBar(info.GlobalPercent, 0, 8, new CornerRadius(4), 0xCC);
		globalBar.HorizontalAlignment = HorizontalAlignment.Stretch;
		globalBar.Margin = new Thickness(0, 0, 0, 10);
		left.Children.Add(globalBar);

		foreach (string region in Regions)
		{
			double pct = info.RegionPercents.TryGetValue(region, out double p) ? p : 0;
			left.Children.Add(BuildRegionRow(region, pct, info.ActiveRegion == region));
		}

		string regionDisplay = Loc.RegionDisplayName(info.ActiveRegion ?? "Kanto");
		string zoneTitle = info.ActiveZoneTitle ?? "";
		left.Children.Add(new TextBlock
		{
			Text = $"{regionDisplay} - {zoneTitle}",
			FontSize = 11,
			Foreground = new SolidColorBrush(Color.FromArgb(0xAA, 0xDD, 0xDD, 0xDD)),
			Margin = new Thickness(0, 10, 0, 4),
			TextWrapping = TextWrapping.Wrap,
			FontFamily = _poppins
		});

		if (!string.IsNullOrEmpty(info.CurrentQuest))
		{
			left.Children.Add(new TextBlock
			{
				Text = $"— {info.CurrentQuest}",
				FontSize = 10,
				Foreground = new SolidColorBrush(Color.FromArgb(0x99, 0xDD, 0xDD, 0xDD)),
				TextWrapping = TextWrapping.Wrap,
				FontFamily = _poppins,
				LineHeight = 14
			});
		}

		StackPanel chips = new StackPanel
		{
			Orientation = Orientation.Horizontal,
			Margin = new Thickness(0, 12, 0, 0)
		};
		Button playBtn = MakePlayChip(info.Name);
		chips.Children.Add(playBtn);
		chips.Children.Add(renameBtn);
		navItems.Add(playBtn);
		navItems.Add(renameBtn);

		foreach (string region in Regions)
		{
			bool active = info.ActiveRegion == region;
			Button regionChip = MakeRegionChip(region, info.Name, active);
			chips.Children.Add(regionChip);
			navItems.Add(regionChip);
		}

		Button avatarBtn = MakeAvatarPickButton(info.Name, navItems);
		chips.Children.Add(avatarBtn);
		left.Children.Add(chips);

		Grid.SetColumn(left, 0);
		body.Children.Add(left);

		Image avatarImage = new Image
		{
			Source = AppAssets.GetAvatarFull(avatarId),
			Stretch = Stretch.Uniform,
			HorizontalAlignment = HorizontalAlignment.Center,
			VerticalAlignment = VerticalAlignment.Bottom,
			Height = 220
		};

		StackPanel avatarCol = new StackPanel
		{
			VerticalAlignment = VerticalAlignment.Center,
			HorizontalAlignment = HorizontalAlignment.Center
		};
		avatarCol.Children.Add(new Border
		{
			Width = HubAvatarColumnWidth - 8,
			Height = 220,
			Background = Brushes.Transparent,
			Child = avatarImage
		});

		Grid.SetColumn(avatarCol, 1);
		body.Children.Add(avatarCol);

		card.Child = body;
		_cardNavGroups.Add(navItems);
		_cardBorders.Add(card);

		card.MouseLeftButtonDown += delegate (object s, MouseButtonEventArgs e)
		{
			if (e.OriginalSource is Button) return;
			OpenTracker(info.Name, CharacterPrefs.GetLastRegion(info.Name));
		};

		return card;
	}

	private Button MakePlayChip(string charName)
	{
		Button chip = new Button
		{
			Content = "▶",
			FontSize = 12,
			Padding = new Thickness(10, 5, 10, 5),
			Margin = new Thickness(0, 0, 6, 0),
			Cursor = Cursors.Hand,
			FontFamily = _poppins,
			Foreground = Brushes.White,
			BorderThickness = new Thickness(1),
			Focusable = true,
			IsTabStop = false,
			ToolTip = Loc.PlayToolTip,
			Background = new SolidColorBrush(Color.FromArgb(0x77, 0x3D, 0x6B, 0x9E)),
			BorderBrush = new SolidColorBrush(Color.FromArgb(0x88, 0x6A, 0x9F, 0xD4))
		};
		chip.Resources.Add(typeof(Border), new Style(typeof(Border))
		{
			Setters = { new Setter(Border.CornerRadiusProperty, new CornerRadius(6)) }
		});
		chip.Click += delegate { OpenTracker(charName, CharacterPrefs.GetLastRegion(charName)); };
		RememberChipStyle(chip);
		return chip;
	}

	private Button MakeAvatarPickButton(string charName, List<FrameworkElement> navItems)
	{
		Button btn = new Button
		{
			Width = 40,
			Height = 40,
			Padding = new Thickness(3),
			Cursor = Cursors.Hand,
			Focusable = true,
			IsTabStop = false,
			ToolTip = Loc.AvatarPickToolTip,
			BorderThickness = new Thickness(1),
			Background = new SolidColorBrush(Color.FromArgb(0x55, 0x33, 0x33, 0x33)),
			BorderBrush = new SolidColorBrush(Color.FromArgb(0x55, 0x88, 0x88, 0x88)),
			Content = new Image
			{
				Source = AppAssets.GetAvatarCropped(CharacterPrefs.GetAvatar(charName)),
				Stretch = Stretch.UniformToFill,
				Width = 32,
				Height = 32
			}
		};
		btn.Resources.Add(typeof(Border), new Style(typeof(Border))
		{
			Setters = { new Setter(Border.CornerRadiusProperty, new CornerRadius(8)) }
		});
		btn.Click += delegate
		{
			var snap = CaptureHubNav();
			SuspendPadNav();
			var picker = new AvatarPickerWindow(CharacterPrefs.GetAvatar(charName), this);
			if (picker.ShowDialog() == true)
			{
				CharacterPrefs.SetAvatar(charName, picker.SelectedAvatarId);
				BuildCharacterCards();
			}
			ResumePadNav(snap);
		};
		navItems.Add(btn);
		RememberChipStyle(btn);
		return btn;
	}

	private Button MakeRegionChip(string region, string charName, bool isActive)
	{
		Button chip = new Button
		{
			Content = Loc.RegionDisplayName(region),
			FontSize = 10,
			Padding = new Thickness(10, 5, 10, 5),
			Margin = new Thickness(0, 0, 6, 0),
			Cursor = Cursors.Hand,
			FontFamily = _poppins,
			Foreground = Brushes.White,
			Focusable = true,
			IsTabStop = false,
			BorderThickness = new Thickness(1),
			Background = isActive
				? new SolidColorBrush(Color.FromArgb(0x55, 0x3D, 0x6B, 0x9E))
				: new SolidColorBrush(Color.FromArgb(0x66, 0x22, 0x22, 0x22)),
			BorderBrush = isActive
				? new SolidColorBrush(Color.FromArgb(0x88, 0x6A, 0x9F, 0xD4))
				: new SolidColorBrush(Color.FromArgb(0x55, 0x88, 0x88, 0x88))
		};
		chip.Resources.Add(typeof(Border), new Style(typeof(Border))
		{
			Setters = { new Setter(Border.CornerRadiusProperty, new CornerRadius(6)) }
		});
		chip.Click += delegate { OpenTracker(charName, region); };
		RememberChipStyle(chip);
		return chip;
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
		Grid outer = new Grid { Height = height, Margin = new Thickness(0, 0, 0, 4) };
		if (width > 0)
			outer.Width = width;
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
		Grid row = new Grid { Margin = new Thickness(0, 3, 0, 3) };
		row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(64) });
		row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
		row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(40) });

		TextBlock label = new TextBlock
		{
			Text = Loc.RegionDisplayName(region),
			FontSize = 11,
			FontFamily = _poppins,
			Foreground = isActive
				? new SolidColorBrush(Color.FromArgb(0xFF, 0xA0, 0xD0, 0xFF))
				: new SolidColorBrush(Color.FromArgb(0x99, 0xDD, 0xDD, 0xDD)),
			VerticalAlignment = VerticalAlignment.Center
		};
		Grid.SetColumn(label, 0);

		Grid barHost = new Grid { Height = 6, Margin = new Thickness(6, 0, 6, 0) };
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
			FontSize = 11,
			FontFamily = _poppins,
			Foreground = isActive
				? new SolidColorBrush(Color.FromArgb(0xCC, 0xA0, 0xD0, 0xFF))
				: new SolidColorBrush(Color.FromArgb(0x88, 0xDD, 0xDD, 0xDD)),
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
		var snap = CaptureHubNav();
		SuspendPadNav();
		new SettingsWindow().ShowDialog();
		ResumePadNav(snap);
		BuildCharacterCards();
	}
}
