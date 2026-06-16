using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
using System.Windows.Threading;
using SDL2;

namespace PokeMMOTracker;

/// <summary>
/// D-pad navigation for hub / dialogs — same edge-detection model as MainWindow tracker nav.
/// Groups are rows: Up/Down switches row, Left/Right moves within a row (or grid cell).
/// </summary>
public sealed class ControllerGroupNav : IDisposable
{
	private sealed class Item
	{
		public FrameworkElement Element;
		public Action Activate;
		public Brush BorderBrushNormal;
		public Brush BackgroundNormal;
		public Thickness BorderThicknessNormal;
	}

	private sealed class Group
	{
		public List<Item> Items = new();
		public int GridColumns = 0;
		public FrameworkElement? ScrollAnchor;
		public Brush? ScrollAnchorBorderNormal;
		public Thickness ScrollAnchorBorderThicknessNormal;
		public bool ResetIndexOnVerticalEnter;
	}

	private readonly List<Group> _groups = new();
	private readonly DispatcherTimer _timer;
	private IntPtr _controller = IntPtr.Zero;
	private readonly bool _ownsController;
	private int _groupIndex = 0;
	private int _itemIndex = 0;
	private int _lastVisualGroupIndex = -1;
	private Item? _lastHighlightedItem;
	private bool _enabled = true;

	private bool _prevA, _prevB;
	private bool _prevUp, _prevDown, _prevLeft, _prevRight;
	private bool _actionArmed = false;

	public Action? BackAction { get; set; }

	public event Action<FrameworkElement, bool>? SelectionChanged;
	public event Action<FrameworkElement?>? ItemHighlighted;

	public ControllerGroupNav() : this(IntPtr.Zero) { }

	public ControllerGroupNav(IntPtr existingController)
	{
		_controller = existingController;
		_ownsController = existingController == IntPtr.Zero;

		try { SDL.SDL_Init(SDL.SDL_INIT_GAMECONTROLLER); } catch { }

		_timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(32) };
		_timer.Tick += OnTick;
		_timer.Start();
	}

	public void SetEnabled(bool enabled)
	{
		_enabled = enabled;
		if (!enabled)
			ClearAllVisuals();
	}

	public void Clear()
	{
		ClearAllVisuals();
		_groups.Clear();
		_groupIndex = 0;
		_itemIndex = 0;
		_lastVisualGroupIndex = -1;
		_lastHighlightedItem = null;
		ResetInputBaseline();
	}

	public void AddGroup(params (FrameworkElement element, Action activate)[] items)
	{
		AddGroup((IEnumerable<(FrameworkElement, Action)>)items);
	}

	public void AddGroup(IEnumerable<(FrameworkElement element, Action activate)> items)
	{
		var row = new Group();
		foreach (var (element, activate) in items)
		{
			if (element == null) continue;
			row.Items.Add(CreateItem(element, activate));
		}
		if (row.Items.Count > 0)
			_groups.Add(row);
	}

	public void AddCardGroup(FrameworkElement scrollAnchor, IEnumerable<(FrameworkElement element, Action activate)> items)
	{
		var row = new Group { ScrollAnchor = scrollAnchor, ResetIndexOnVerticalEnter = true };
		if (scrollAnchor is Border b)
		{
			row.ScrollAnchorBorderNormal = b.BorderBrush;
			row.ScrollAnchorBorderThicknessNormal = b.BorderThickness;
		}
		foreach (var (element, activate) in items)
		{
			if (element == null) continue;
			row.Items.Add(CreateItem(element, activate));
		}
		if (row.Items.Count > 0)
			_groups.Add(row);
	}

	public void AddGrid(int columns, IEnumerable<(FrameworkElement element, Action activate)> items)
	{
		if (columns < 1) columns = 1;
		var grid = new Group { GridColumns = columns };
		foreach (var (element, activate) in items)
		{
			if (element == null) continue;
			grid.Items.Add(CreateItem(element, activate));
		}
		if (grid.Items.Count > 0)
			_groups.Add(grid);
	}

	public void AddGroup(FrameworkElement element, Action activate)
	{
		AddGroup(new[] { (element, activate) });
	}

	public void AddButton(Button button)
	{
		AddGroup(button, () => button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent)));
	}

	public void AddCheckBox(CheckBox box)
	{
		AddGroup(box, () => box.IsChecked = !box.IsChecked);
	}

	public void AddBorder(Border border, Action activate)
	{
		AddGroup(border, activate);
	}

	public void FocusFirst()
	{
		if (_groups.Count == 0) return;
		_groupIndex = 0;
		_itemIndex = 0;
		_lastVisualGroupIndex = -1;
		ResetInputBaseline();
		UpdateSelectionVisuals();
	}

	public void FocusAt(int groupIndex, int itemIndex)
	{
		if (_groups.Count == 0) return;
		_groupIndex = Math.Clamp(groupIndex, 0, _groups.Count - 1);
		_itemIndex = Math.Clamp(itemIndex, 0, _groups[_groupIndex].Items.Count - 1);
		_lastVisualGroupIndex = -1;
		ResetInputBaseline();
		UpdateSelectionVisuals();
	}

	public (int Group, int Item) GetFocus() => (_groupIndex, _itemIndex);

	private void ResetInputBaseline()
	{
		_actionArmed = false;
		_prevA = false;
		_prevB = false;
		_prevUp = false;
		_prevDown = false;
		_prevLeft = false;
		_prevRight = false;
	}

	public void Dispose()
	{
		_timer.Stop();
		ClearAllVisuals();
		if (_ownsController && _controller != IntPtr.Zero)
		{
			try { SDL.SDL_GameControllerClose(_controller); } catch { }
			_controller = IntPtr.Zero;
		}
	}

	private static Item CreateItem(FrameworkElement element, Action activate)
	{
		var item = new Item { Element = element, Activate = activate };

		if (element is Border b)
		{
			item.BorderBrushNormal = b.BorderBrush;
			item.BackgroundNormal = b.Background;
			item.BorderThicknessNormal = b.BorderThickness;
		}
		else if (element is Button btn)
		{
			item.BorderBrushNormal = btn.BorderBrush;
			item.BackgroundNormal = btn.Background;
			item.BorderThicknessNormal = btn.BorderThickness;
		}
		else if (element is CheckBox)
		{
			item.BorderBrushNormal = Brushes.Transparent;
			item.BackgroundNormal = Brushes.Transparent;
			item.BorderThicknessNormal = new Thickness(0);
		}
		else
		{
			item.BorderBrushNormal = Brushes.Transparent;
			item.BackgroundNormal = Brushes.Transparent;
			item.BorderThicknessNormal = new Thickness(0);
		}

		return item;
	}

	private void OnTick(object sender, EventArgs e)
	{
		if (!_enabled || _groups.Count == 0) return;

		try
		{
			SDL.SDL_PumpEvents();
			EnsureController();
			if (_controller == IntPtr.Zero ||
			    SDL.SDL_GameControllerGetAttached(_controller) != SDL.SDL_bool.SDL_TRUE)
				return;

			bool upPressed = Btn(SDL.SDL_GameControllerButton.SDL_CONTROLLER_BUTTON_DPAD_UP);
			bool downPressed = Btn(SDL.SDL_GameControllerButton.SDL_CONTROLLER_BUTTON_DPAD_DOWN);
			bool leftPressed = Btn(SDL.SDL_GameControllerButton.SDL_CONTROLLER_BUTTON_DPAD_LEFT);
			bool rightPressed = Btn(SDL.SDL_GameControllerButton.SDL_CONTROLLER_BUTTON_DPAD_RIGHT);
			bool aPressed = Btn(SDL.SDL_GameControllerButton.SDL_CONTROLLER_BUTTON_A);
			bool bPressed = Btn(SDL.SDL_GameControllerButton.SDL_CONTROLLER_BUTTON_B);

			if (!_actionArmed && !aPressed && !bPressed)
				_actionArmed = true;

			Group group = _groups[_groupIndex];
			bool moved = group.GridColumns > 0
				? ProcessGridEdges(upPressed, downPressed, leftPressed, rightPressed)
				: ProcessRowEdges(upPressed, downPressed, leftPressed, rightPressed);

			if (moved)
				UpdateSelectionVisuals();

			if (aPressed && !_prevA && _actionArmed)
			{
				Item item = _groups[_groupIndex].Items[_itemIndex];
				try { item.Activate?.Invoke(); }
				catch (Exception ex) { TrackerLog.Error("Controller nav activate: " + ex); }
			}

			if (bPressed && !_prevB && _actionArmed && BackAction != null)
			{
				try { BackAction(); }
				catch (Exception ex) { TrackerLog.Error("Controller nav back: " + ex); }
			}

			_prevA = aPressed;
			_prevB = bPressed;
			_prevUp = upPressed;
			_prevDown = downPressed;
			_prevLeft = leftPressed;
			_prevRight = rightPressed;
		}
		catch { }
	}

	private bool ProcessRowEdges(bool upPressed, bool downPressed, bool leftPressed, bool rightPressed)
	{
		if (downPressed && !_prevDown)
		{
			if (_groupIndex < _groups.Count - 1)
			{
				_groupIndex++;
				ApplyVerticalGroupEntry(_groups[_groupIndex]);
				return true;
			}
		}

		if (upPressed && !_prevUp)
		{
			if (_groupIndex > 0)
			{
				_groupIndex--;
				ApplyVerticalGroupEntry(_groups[_groupIndex]);
				return true;
			}
		}

		Group g = _groups[_groupIndex];

		if (rightPressed && !_prevRight && g.Items.Count > 1 && _itemIndex < g.Items.Count - 1)
		{
			_itemIndex++;
			return true;
		}

		if (leftPressed && !_prevLeft && g.Items.Count > 1 && _itemIndex > 0)
		{
			_itemIndex--;
			return true;
		}

		return false;
	}

	private bool ProcessGridEdges(bool upPressed, bool downPressed, bool leftPressed, bool rightPressed)
	{
		Group group = _groups[_groupIndex];
		int cols = group.GridColumns;
		int count = group.Items.Count;
		int rows = (count + cols - 1) / cols;
		int row = _itemIndex / cols;
		int col = _itemIndex % cols;

		if (downPressed && !_prevDown)
		{
			if (row < rows - 1 && _itemIndex + cols < count)
			{
				_itemIndex += cols;
				return true;
			}
			if (_groupIndex < _groups.Count - 1)
			{
				_groupIndex++;
				ApplyVerticalGroupEntry(_groups[_groupIndex], col);
				return true;
			}
		}

		if (upPressed && !_prevUp)
		{
			if (row > 0)
			{
				_itemIndex -= cols;
				return true;
			}
			if (_groupIndex > 0)
			{
				_groupIndex--;
				ApplyVerticalGroupEntryFromGrid(_groups[_groupIndex], col);
				return true;
			}
		}

		if (rightPressed && !_prevRight && col < cols - 1 && _itemIndex + 1 < count)
		{
			_itemIndex++;
			return true;
		}

		if (leftPressed && !_prevLeft && col > 0)
		{
			_itemIndex--;
			return true;
		}

		return false;
	}

	private void ApplyVerticalGroupEntry(Group group, int preferredIndex = 0)
	{
		if (group.ResetIndexOnVerticalEnter)
			_itemIndex = 0;
		else
			_itemIndex = Math.Min(preferredIndex, group.Items.Count - 1);
	}

	private void ApplyVerticalGroupEntryFromGrid(Group group, int col)
	{
		if (group.ResetIndexOnVerticalEnter)
		{
			_itemIndex = 0;
			return;
		}

		if (group.GridColumns > 0)
		{
			int cols = group.GridColumns;
			int rows = (group.Items.Count + cols - 1) / cols;
			_itemIndex = (rows - 1) * cols + col;
			if (_itemIndex >= group.Items.Count)
				_itemIndex = group.Items.Count - 1;
		}
		else
			_itemIndex = Math.Min(col, group.Items.Count - 1);
	}

	private bool Btn(SDL.SDL_GameControllerButton button)
	{
		return SDL.SDL_GameControllerGetButton(_controller, button) == 1;
	}

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

	private void ClearAllVisuals()
	{
		_lastHighlightedItem = null;
		foreach (Group group in _groups)
			ClearGroupVisuals(group);
		try { ItemHighlighted?.Invoke(null); } catch { }
	}

	private void ClearGroupVisuals(Group group)
	{
		if (group.ScrollAnchor is Border card && group.ScrollAnchorBorderNormal != null)
		{
			card.BorderBrush = group.ScrollAnchorBorderNormal;
			card.BorderThickness = group.ScrollAnchorBorderThicknessNormal;
			card.Effect = null;
		}

		if (group.ScrollAnchor == null)
		{
			foreach (Item item in group.Items)
				ApplyNormal(item);
		}
	}

	private void UpdateSelectionVisuals()
	{
		if (_groupIndex < 0 || _groupIndex >= _groups.Count) return;
		Group group = _groups[_groupIndex];
		if (_itemIndex < 0 || _itemIndex >= group.Items.Count) return;

		bool isCardGroup = group.ScrollAnchor != null;
		bool isNewGroup = _groupIndex != _lastVisualGroupIndex;

		if (isNewGroup)
		{
			if (_lastVisualGroupIndex >= 0 && _lastVisualGroupIndex < _groups.Count)
				ClearGroupVisuals(_groups[_lastVisualGroupIndex]);
		}
		else if (!isCardGroup && _lastHighlightedItem != null)
			ApplyNormal(_lastHighlightedItem);

		Item selected = group.Items[_itemIndex];
		if (!isCardGroup)
			ApplySelected(selected);
		_lastHighlightedItem = selected;

		if (isCardGroup && group.ScrollAnchor is Border cardBorder)
		{
			cardBorder.BorderThickness = new Thickness(2);
			cardBorder.BorderBrush = new SolidColorBrush(Color.FromArgb(0xCC, 0xA0, 0xD0, 0xFF));
			cardBorder.Effect = null;
		}

		_lastVisualGroupIndex = _groupIndex;

		try { ItemHighlighted?.Invoke(isCardGroup ? selected.Element : null); } catch { }

		if (isNewGroup && group.ScrollAnchor != null)
		{
			try { SelectionChanged?.Invoke(group.ScrollAnchor, true); }
			catch { }
		}
	}

	private static void ApplySelected(Item item)
	{
		FrameworkElement el = item.Element;

		if (el is Border b)
		{
			b.BorderThickness = new Thickness(2);
			b.BorderBrush = Brushes.White;
		}
		else if (el is Button btn)
		{
			btn.BorderThickness = new Thickness(2);
			btn.BorderBrush = Brushes.White;
			btn.Background = new SolidColorBrush(Color.FromArgb(0x88, 0x3D, 0x6B, 0x9E));
		}

		ApplyPulsingGlow(el);
	}

	private static void ApplyNormal(Item item)
	{
		FrameworkElement el = item.Element;
		el.Effect = null;

		if (el is Border b)
		{
			b.BorderBrush = item.BorderBrushNormal;
			b.Background = item.BackgroundNormal;
			b.BorderThickness = item.BorderThicknessNormal;
		}
		else if (el is Button btn)
		{
			btn.BorderBrush = item.BorderBrushNormal;
			btn.Background = item.BackgroundNormal;
			btn.BorderThickness = item.BorderThicknessNormal;
		}
	}

	private static void ApplyPulsingGlow(UIElement el)
	{
		if (el == null) return;
		var glow = new DropShadowEffect { Color = Colors.White, ShadowDepth = 0, BlurRadius = 10, Opacity = 0.4 };
		el.Effect = glow;
		var ease = new SineEase { EasingMode = EasingMode.EaseInOut };
		var blur = new DoubleAnimation(8, 20, TimeSpan.FromMilliseconds(950))
		{
			AutoReverse = true,
			RepeatBehavior = RepeatBehavior.Forever,
			EasingFunction = ease
		};
		var op = new DoubleAnimation(0.35, 0.7, TimeSpan.FromMilliseconds(950))
		{
			AutoReverse = true,
			RepeatBehavior = RepeatBehavior.Forever,
			EasingFunction = ease
		};
		glow.BeginAnimation(DropShadowEffect.BlurRadiusProperty, blur);
		glow.BeginAnimation(DropShadowEffect.OpacityProperty, op);
	}
}
