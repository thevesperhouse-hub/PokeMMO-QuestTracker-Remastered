using System;
using System.Collections.Generic;
using System.Data.SQLite;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using NHotkey;
using NHotkey.Wpf;
using PokeMMOTracker;
using PokeMMOTracker.Models;
using PokeMMOTracker.Properties;
using SDL2;

namespace PokeMMOTracker.Views;

public partial class MainWindow : Window, IComponentConnector
{
	[DllImport("user32.dll")]
	[return: MarshalAs(UnmanagedType.Bool)]
	static extern bool SetForegroundWindow(IntPtr hWnd);

	[DllImport("user32.dll")]
	[return: MarshalAs(UnmanagedType.Bool)]
	static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);

	[DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW")]
	static extern IntPtr GetWindowLongPtr(IntPtr hWnd, int nIndex);

	[DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW")]
	static extern IntPtr SetWindowLongPtr(IntPtr hWnd, int nIndex, IntPtr dwNewLong);

	[DllImport("user32.dll")]
	static extern IntPtr MonitorFromWindow(IntPtr hwnd, uint dwFlags);

	[DllImport("user32.dll", EntryPoint = "GetMonitorInfoW")]
	[return: MarshalAs(UnmanagedType.Bool)]
	static extern bool GetMonitorInfo(IntPtr hMonitor, ref NativeMonitorInfo lpmi);

	[DllImport("user32.dll")]
	static extern IntPtr GetForegroundWindow();

	[DllImport("user32.dll")]
	static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

	[DllImport("kernel32.dll")]
	static extern uint GetCurrentThreadId();

	[DllImport("user32.dll")]
	[return: MarshalAs(UnmanagedType.Bool)]
	static extern bool AttachThreadInput(uint idAttach, uint idAttachTo, bool fAttach);

	[DllImport("user32.dll")]
	[return: MarshalAs(UnmanagedType.Bool)]
	static extern bool BringWindowToTop(IntPtr hWnd);

	[DllImport("user32.dll")]
	[return: MarshalAs(UnmanagedType.Bool)]
	static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

	[DllImport("user32.dll", SetLastError = true)]
	[return: MarshalAs(UnmanagedType.Bool)]
	static extern bool SystemParametersInfo(uint uiAction, uint uiParam, IntPtr pvParam, uint fWinIni);

	[DllImport("user32.dll")]
	static extern void keybd_event(byte bVk, byte bScan, uint dwFlags, UIntPtr dwExtraInfo);

	[DllImport("user32.dll")]
	[return: MarshalAs(UnmanagedType.Bool)]
	static extern bool GetCursorPos(out NativePoint lpPoint);

	[DllImport("user32.dll")]
	[return: MarshalAs(UnmanagedType.Bool)]
	static extern bool SetCursorPos(int X, int Y);

	[DllImport("user32.dll")]
	[return: MarshalAs(UnmanagedType.Bool)]
	static extern bool GetWindowRect(IntPtr hWnd, out NativeRect lpRect);

	[DllImport("user32.dll")]
	static extern void mouse_event(uint dwFlags, uint dx, uint dy, uint dwData, UIntPtr dwExtraInfo);

	[StructLayout(LayoutKind.Sequential)]
	private struct NativePoint { public int X, Y; }

	private const uint MOUSEEVENTF_LEFTDOWN = 0x0002;
	private const uint MOUSEEVENTF_LEFTUP = 0x0004;

	private const int SW_SHOW = 5;
	private const uint SPI_SETFOREGROUNDLOCKTIMEOUT = 0x2001;
	private const uint SPIF_SENDCHANGE = 0x0002;
	private const byte VK_MENU = 0x12;
	private const uint KEYEVENTF_KEYUP = 0x0002;

	[StructLayout(LayoutKind.Sequential)]
	private struct NativeRect { public int Left, Top, Right, Bottom; }

	[StructLayout(LayoutKind.Sequential)]
	private struct NativeMonitorInfo { public int cbSize; public NativeRect rcMonitor; public NativeRect rcWork; public uint dwFlags; }

	private static readonly IntPtr HWND_TOPMOST = new IntPtr(-1);
	private const uint SWP_NOSIZE = 0x0001;
	private const uint SWP_NOMOVE = 0x0002;
	private const uint SWP_NOZORDER = 0x0004;
	private const uint SWP_FRAMECHANGED = 0x0020;
	private const uint SWP_NOACTIVATE = 0x0010;
	private const uint SWP_SHOWWINDOW = 0x0040;

	private const int GWL_STYLE = -16;
	private const long WS_CAPTION = 0x00C00000L;
	private const long WS_THICKFRAME = 0x00040000L;
	private const long WS_MINIMIZEBOX = 0x00020000L;
	private const long WS_MAXIMIZEBOX = 0x00010000L;
	private const long WS_SYSMENU = 0x00080000L;
	private const long WS_POPUP = unchecked((long)0x80000000L);
	private const long WS_VISIBLE = 0x10000000L;
	private const uint MONITOR_DEFAULTTONEAREST = 2;

	// Re-asserts the topmost z-order without stealing focus, so the overlay
	// stays in front of borderless/fullscreen PokeMMO on machines whose GPU
	// presentation model demotes WPF's one-shot Topmost flag.
	private DispatcherTimer topmostTimer;

	private static readonly KeyGesture BorderlessGesture = new KeyGesture(Key.B, ModifierKeys.Control | ModifierKeys.Shift);

	private readonly string _dbPath;
	private readonly string _charName;
	private readonly string _regionName;

	private StackPanel tasksPanel;
	private ScrollViewer scrollViewer;

	private static readonly KeyGesture PreviousGesture = new KeyGesture(Key.Left, ModifierKeys.Control);
	private static readonly KeyGesture NextGesture = new KeyGesture(Key.Right, ModifierKeys.Control);

	// Configurable controller buttons for the global validate/undo shortcuts.
	private int _checkButton = (int)SDL.SDL_GameControllerButton.SDL_CONTROLLER_BUTTON_RIGHTSHOULDER;
	private int _uncheckButton = (int)SDL.SDL_GameControllerButton.SDL_CONTROLLER_BUTTON_LEFTSHOULDER;
	// Pauses controller handling while the bind capture window is open.
	private bool _suspendController = false;

	private bool scrollEnabled = Settings.Default.RememberScroll;

	// Universal Controller Logic (SDL2)
	private DispatcherTimer controllerTimer;
	private IntPtr gameController = IntPtr.Zero;
	private bool isTrackerModeActive = false;
	
	// Controller Mode UI State
	private int selectedTaskIndex = 0;
	private int totalTasksCount = 0;
	private List<Border> taskBorders = new List<Border>();
	private List<CheckBox> taskCheckBoxes = new List<CheckBox>();
	
	// Bottom Buttons
	private List<Button> bottomButtons = new List<Button>();
	private bool isNavigatingBottomButtons = false;
	private int selectedButtonIndex = 0; // 0: Prev, 1: Switch, 2: Next

	private bool previousL3R3State = false;
	private bool previousAState = false;
	private bool previousBState = false;
	private bool previousRBState = false;
	private bool previousLBState = false;
	private bool previousGlobalRBState = false;
	private bool previousGlobalLBState = false;
	private bool _recenterOnRebuild = false;
	private double _scrollOffsetBeforeRebuild = 0;
	private int _scrollUiToken = 0;
	private bool _firstBuild = true;            // staggered entrance only on first render
	private string _flashLabel = null;          // quest label to flash green after a rebuild
	private Border _flashTargetBorder = null;   // resolved during the rebuild
	private Color _flashBaseColor = Colors.Transparent;
	private bool previousUpState = false;
	private bool previousDownState = false;
	private bool previousLeftState = false;
	private bool previousRightState = false;

	private int _viewMode = 0; // 0 full, 1 compact, 2 minimal
	private DispatcherTimer _geometrySaveTimer;

	// Header buttons (view toggle, bind) — controller-navigable.
	private List<Button> headerButtons = new List<Button>();
	private bool isNavigatingHeaderButtons = false;
	private int selectedHeaderIndex = 0;
	private bool previousStartState = false;
	private bool previousYState = false;
	private bool _pulseRegionBar = false;
	private bool _scheduleNarrateAfterLayout = false;

	public MainWindow(string charName, string regionName)
	{
		ApplyKeyboardBinds();
		ApplyControllerBinds();
		HotkeyManager.Current.AddOrReplace("Previous", PreviousGesture, PreviousPage);
		HotkeyManager.Current.AddOrReplace("Next", NextGesture, NextPage);
		HotkeyManager.Current.AddOrReplace("Borderless", BorderlessGesture, BorderlessHotkey);
		InitializeComponent();
		Icon = BitmapFrame.Create((BitmapSource)AppAssets.AppIcon);
		base.DataContext = this;
		base.Topmost = true;
		base.Opacity = 0.0; // faded in on load for a smooth entrance
		base.StateChanged += OnStateChanged;
		base.Loaded += MainWindow_Loaded;
		base.Closed += MainWindow_Closed;
		base.LocationChanged += delegate { ScheduleGeometrySave(); };
		base.SizeChanged += delegate { ScheduleGeometrySave(); };

		_charName = charName;
		_regionName = regionName;
		_dbPath = DatabaseHelper.GetDatabasePath();
		CharacterPrefs.SetLastRegion(_charName, _regionName);
		_viewMode = Settings.Default.ViewMode;
		if (Settings.Default.CompactMode && _viewMode == 0)
		{
			_viewMode = 1;
			Settings.Default.CompactMode = false;
			Settings.Default.ViewMode = 1;
			Settings.Default.Save();
		}
		if (_viewMode < 0 || _viewMode > 2) _viewMode = 0;

		RestoreWindowGeometry();
		if (WindowStartupLocation != WindowStartupLocation.Manual)
		{
			WindowStartupLocation = WindowStartupLocation.CenterScreen;
			base.Width = _viewMode >= 1 ? 380.0 : 450.0;
			base.Height = _viewMode == 2 ? 220.0 : (_viewMode == 1 ? 260.0 : 600.0);
		}
		BuildUI();

		try 
		{
			SDL.SDL_Init(SDL.SDL_INIT_GAMECONTROLLER);
			controllerTimer = new DispatcherTimer();
			controllerTimer.Interval = TimeSpan.FromMilliseconds(32); 
			controllerTimer.Tick += ControllerTimer_Tick;
			controllerTimer.Start();
		}
		catch (Exception ex)
		{
			TrackerLog.Error("SDL controller init failed: " + ex);
		}
	}

	private void FocusPokeMMO()
	{
		IntPtr hwnd = FindPokeMMOWindow();
		if (hwnd == IntPtr.Zero) return;

		// PokeMMO only re-grabs the controller on a REAL activation. The Win32
		// foreground calls are silently refused for another process, so we
		// reproduce what actually works: a genuine mouse click on its window. Do it
		// while PokeMMO is still inactive so Windows "eats" the click for activation
		// (not passed to the game). The cursor is restored so it's invisible.
		ActivatePokeMMOByClick(hwnd);
		ForceForeground(hwnd); // safety net (no-op if the click already activated it)
	}

	private void ActivatePokeMMOByClick(IntPtr hwnd)
	{
		try
		{
			if (!GetWindowRect(hwnd, out NativeRect r)) return;
			if (!GetCursorPos(out NativePoint saved)) return;

			// Click near the top-left corner: most likely dead space / HUD, and in
			// any case the activating click is consumed by Windows, not the game.
			int targetX = r.Left + 5;
			int targetY = r.Top + 5;

			SetCursorPos(targetX, targetY);
			mouse_event(MOUSEEVENTF_LEFTDOWN, 0, 0, 0, UIntPtr.Zero);
			mouse_event(MOUSEEVENTF_LEFTUP, 0, 0, 0, UIntPtr.Zero);
			SetCursorPos(saved.X, saved.Y);
		}
		catch { }
	}

	// Robustly brings a window to the foreground WITHOUT the Windows foreground
	// lock kicking in (which would otherwise just flash the taskbar button and
	// play the notification sound). Used to hand input focus between PokeMMO and
	// the tracker so the controller doesn't drive both at once.
	private void ForceForeground(IntPtr hWnd)
	{
		try
		{
			if (hWnd == IntPtr.Zero) return;
			IntPtr foreground = GetForegroundWindow();
			if (foreground == hWnd) return; // already focused

			// Disable the foreground lock so SetForegroundWindow always wins instead
			// of being downgraded to a taskbar flash / no-op.
			SystemParametersInfo(SPI_SETFOREGROUNDLOCKTIMEOUT, 0, IntPtr.Zero, SPIF_SENDCHANGE);

			// Inject a harmless ALT tap so Windows treats this as a real user-input
			// event. Without it, SetForegroundWindow on ANOTHER process's window is
			// silently refused (the window comes up only on a real mouse click).
			keybd_event(VK_MENU, 0, 0, UIntPtr.Zero);
			keybd_event(VK_MENU, 0, KEYEVENTF_KEYUP, UIntPtr.Zero);

			uint currentThread = GetCurrentThreadId();
			uint foreThread = foreground != IntPtr.Zero ? GetWindowThreadProcessId(foreground, out _) : 0;
			bool attached = foreThread != 0 && foreThread != currentThread && AttachThreadInput(currentThread, foreThread, true);

			ShowWindow(hWnd, SW_SHOW);
			BringWindowToTop(hWnd);
			SetForegroundWindow(hWnd);

			if (attached) AttachThreadInput(currentThread, foreThread, false);
		}
		catch { }
	}

	private IntPtr FindPokeMMOWindow()
	{
		int selfId = Process.GetCurrentProcess().Id;
		IntPtr selfHandle = new WindowInteropHelper(this).Handle;
		foreach (Process p in Process.GetProcesses())
		{
			if (p.Id == selfId) continue; // never match the tracker itself
			string title = p.MainWindowTitle;
			if (string.IsNullOrEmpty(title)) continue;
			if (!title.Contains("PokeMMO", StringComparison.OrdinalIgnoreCase)) continue;
			if (title.Contains("Tracker", StringComparison.OrdinalIgnoreCase)) continue; // exclude the tracker window
			if (p.MainWindowHandle != IntPtr.Zero && p.MainWindowHandle != selfHandle)
			{
				return p.MainWindowHandle;
			}
		}
		return IntPtr.Zero;
	}

	private void BorderlessHotkey(object? sender, HotkeyEventArgs e)
	{
		MakePokeMMOBorderless();
	}

	// Strips PokeMMO's window border and stretches it to fill its monitor while
	// keeping it a normal (desktop-composited) window. Unlike PokeMMO's own
	// "Borderless" mode, this never goes exclusive-fullscreen, so the overlay
	// stays in front and the black mode-switch flicker disappears.
	private void MakePokeMMOBorderless()
	{
		try
		{
			IntPtr hwnd = FindPokeMMOWindow();
			if (hwnd == IntPtr.Zero)
			{
				MessageBox.Show("Fenêtre PokeMMO introuvable.\nLance PokeMMO en mode \"Windowed\" (fenêtré), puis réessaie (Ctrl+Shift+B).",
					"PokeMMO Tracker", MessageBoxButton.OK, MessageBoxImage.Information);
				return;
			}

			long style = (long)GetWindowLongPtr(hwnd, GWL_STYLE);
			style &= ~(WS_CAPTION | WS_THICKFRAME | WS_MINIMIZEBOX | WS_MAXIMIZEBOX | WS_SYSMENU);
			style |= WS_POPUP | WS_VISIBLE;
			SetWindowLongPtr(hwnd, GWL_STYLE, new IntPtr(style));

			IntPtr monitor = MonitorFromWindow(hwnd, MONITOR_DEFAULTTONEAREST);
			NativeMonitorInfo mi = new NativeMonitorInfo { cbSize = Marshal.SizeOf(typeof(NativeMonitorInfo)) };
			if (GetMonitorInfo(monitor, ref mi))
			{
				int x = mi.rcMonitor.Left;
				int y = mi.rcMonitor.Top;
				int w = mi.rcMonitor.Right - mi.rcMonitor.Left;
				// Extend 1px below the screen so the window rect no longer EXACTLY
				// matches the monitor. This defeats Windows' "fullscreen window"
				// detection (which would otherwise demote topmost overlays), while
				// remaining visually indistinguishable from true fullscreen.
				int h = (mi.rcMonitor.Bottom - mi.rcMonitor.Top) + 1;
				SetWindowPos(hwnd, IntPtr.Zero, x, y, w, h, SWP_NOZORDER | SWP_FRAMECHANGED | SWP_SHOWWINDOW);
			}

			EnsureTopmost();
		}
		catch { }
	}

	private void FocusTracker()
	{
		IntPtr handle = new WindowInteropHelper(this).Handle;
		ForceForeground(handle);
		EnsureTopmost();
	}

	private void UpdateSelectionVisuals()
	{
		// Reset everything
		for (int i = 0; i < taskBorders.Count; i++)
		{
			taskBorders[i].BorderThickness = new Thickness(0);
			taskBorders[i].BorderBrush = new SolidColorBrush(Colors.Transparent);
			taskBorders[i].Effect = null;
		}
		for (int i = 0; i < bottomButtons.Count; i++)
		{
			bottomButtons[i].BorderThickness = new Thickness(1);
			bottomButtons[i].BorderBrush = new SolidColorBrush(Color.FromArgb(0x55, 0xFF, 0xFF, 0xFF));
			bottomButtons[i].Background = new SolidColorBrush(Color.FromArgb(0x66, 0x1A, 0x1A, 0x1A));
			bottomButtons[i].Effect = null;
		}
		for (int i = 0; i < headerButtons.Count; i++)
		{
			headerButtons[i].BorderThickness = new Thickness(1);
			headerButtons[i].BorderBrush = new SolidColorBrush(Color.FromArgb(0x55, 0xFF, 0xFF, 0xFF));
			headerButtons[i].Background = new SolidColorBrush(Color.FromArgb(0x66, 0x1A, 0x1A, 0x1A));
			headerButtons[i].Effect = null;
		}

		if (!isTrackerModeActive) return;

		if (isNavigatingHeaderButtons)
		{
			if (selectedHeaderIndex >= 0 && selectedHeaderIndex < headerButtons.Count)
			{
				headerButtons[selectedHeaderIndex].BorderThickness = new Thickness(2);
				headerButtons[selectedHeaderIndex].BorderBrush = new SolidColorBrush(Colors.White);
				ApplyPulsingGlow(headerButtons[selectedHeaderIndex]);
			}
			return;
		}

		if (!isNavigatingBottomButtons)
		{
			if (selectedTaskIndex >= 0 && selectedTaskIndex < taskBorders.Count)
			{
				taskBorders[selectedTaskIndex].BorderThickness = new Thickness(2);
				taskBorders[selectedTaskIndex].BorderBrush = new SolidColorBrush(Colors.White);
				ApplyPulsingGlow(taskBorders[selectedTaskIndex]);
				ScrollSelectedIntoView();
			}
		}
		else
		{
			if (selectedButtonIndex >= 0 && selectedButtonIndex < bottomButtons.Count)
			{
				bottomButtons[selectedButtonIndex].BorderThickness = new Thickness(2);
				bottomButtons[selectedButtonIndex].BorderBrush = new SolidColorBrush(Colors.White);
				ApplyPulsingGlow(bottomButtons[selectedButtonIndex]);
				scrollViewer.ScrollToBottom(); // Ensure buttons are visible
			}
		}
	}

	private void ControllerTimer_Tick(object sender, EventArgs e)
	{
		try
		{
			if (_suspendController) return; // bind capture window owns the pad

			SDL.SDL_PumpEvents();

			if (gameController == IntPtr.Zero)
			{
				for (int i = 0; i < SDL.SDL_NumJoysticks(); i++)
				{
					if (SDL.SDL_IsGameController(i) == SDL.SDL_bool.SDL_TRUE)
					{
						gameController = SDL.SDL_GameControllerOpen(i);
						break;
					}
				}
			}

			if (gameController != IntPtr.Zero && SDL.SDL_GameControllerGetAttached(gameController) == SDL.SDL_bool.SDL_TRUE)
			{
				bool l3Pressed = SDL.SDL_GameControllerGetButton(gameController, SDL.SDL_GameControllerButton.SDL_CONTROLLER_BUTTON_LEFTSTICK) == 1;
				bool r3Pressed = SDL.SDL_GameControllerGetButton(gameController, SDL.SDL_GameControllerButton.SDL_CONTROLLER_BUTTON_RIGHTSTICK) == 1;
				bool currentL3R3State = l3Pressed && r3Pressed;

				if (currentL3R3State && !previousL3R3State)
				{
					// Toggle tracker navigation mode. We deliberately do NOT steal
					// window focus: both PokeMMO and the tracker read the controller,
					// and focus juggling proved unreliable for handing control back to
					// PokeMMO. We only bring the overlay visually to the front (no
					// focus steal => no sound, no taskbar flash, no "stuck" game).
					isTrackerModeActive = !isTrackerModeActive;
					if (isTrackerModeActive)
					{
						EnsureTopmost();
					}
					isNavigatingHeaderButtons = false;
					isNavigatingBottomButtons = false;
					UpdateSelectionVisuals();
				}
				previousL3R3State = currentL3R3State;

				// Global quick shortcuts that work WITHOUT entering tracker mode.
				// Buttons are user-configurable (default R1 = validate, L1 = undo).
				bool globalRB = SDL.SDL_GameControllerGetButton(gameController, (SDL.SDL_GameControllerButton)_checkButton) == 1;
				bool globalLB = SDL.SDL_GameControllerGetButton(gameController, (SDL.SDL_GameControllerButton)_uncheckButton) == 1;
				if (!isTrackerModeActive)
				{
					if (globalRB && !previousGlobalRBState) CheckLastUnchecked();
					if (globalLB && !previousGlobalLBState) UncheckLastChecked();
				}
				previousGlobalRBState = globalRB;
				previousGlobalLBState = globalLB;

				bool yPressed = SDL.SDL_GameControllerGetButton(gameController, SDL.SDL_GameControllerButton.SDL_CONTROLLER_BUTTON_Y) == 1;
				if (Settings.Default.NarratorEnabled && yPressed && !previousYState)
					NarrateCurrentQuest();
				previousYState = yPressed;

				bool startPressed = SDL.SDL_GameControllerGetButton(gameController, SDL.SDL_GameControllerButton.SDL_CONTROLLER_BUTTON_START) == 1;
				if (!isTrackerModeActive && startPressed && !previousStartState)
				{
					CycleViewMode();
				}
				previousStartState = startPressed;

				if (isTrackerModeActive)
				{
					// Keep the overlay above a possibly-topmost PokeMMO while
					// navigating, without stealing focus (SWP_NOACTIVATE).
					EnsureTopmost();

					bool upPressed = SDL.SDL_GameControllerGetButton(gameController, SDL.SDL_GameControllerButton.SDL_CONTROLLER_BUTTON_DPAD_UP) == 1;
					bool downPressed = SDL.SDL_GameControllerGetButton(gameController, SDL.SDL_GameControllerButton.SDL_CONTROLLER_BUTTON_DPAD_DOWN) == 1;
					bool leftPressed = SDL.SDL_GameControllerGetButton(gameController, SDL.SDL_GameControllerButton.SDL_CONTROLLER_BUTTON_DPAD_LEFT) == 1;
					bool rightPressed = SDL.SDL_GameControllerGetButton(gameController, SDL.SDL_GameControllerButton.SDL_CONTROLLER_BUTTON_DPAD_RIGHT) == 1;
					bool aPressed = SDL.SDL_GameControllerGetButton(gameController, SDL.SDL_GameControllerButton.SDL_CONTROLLER_BUTTON_A) == 1; 
					bool lbPressed = SDL.SDL_GameControllerGetButton(gameController, SDL.SDL_GameControllerButton.SDL_CONTROLLER_BUTTON_LEFTSHOULDER) == 1;
					bool rbPressed = SDL.SDL_GameControllerGetButton(gameController, SDL.SDL_GameControllerButton.SDL_CONTROLLER_BUTTON_RIGHTSHOULDER) == 1;

					// Shoulder buttons: quick jump to/from the bottom button row (not in minimal mode).
					if (rbPressed && !previousRBState && !isNavigatingBottomButtons && !isNavigatingHeaderButtons
						&& _viewMode < 2 && bottomButtons.Count > 0)
					{
						isNavigatingBottomButtons = true;
						selectedButtonIndex = 0;
						UpdateSelectionVisuals();
					}
					if (lbPressed && !previousLBState && isNavigatingBottomButtons)
					{
						isNavigatingBottomButtons = false;
						UpdateSelectionVisuals();
					}

					// Vertical Navigation
					if (downPressed && !previousDownState)
					{
						if (isNavigatingHeaderButtons)
						{
							isNavigatingHeaderButtons = false;
							selectedTaskIndex = 0;
						}
						else if (!isNavigatingBottomButtons)
						{
							if (selectedTaskIndex < totalTasksCount - 1) 
							{
								selectedTaskIndex++;
							}
							else if (_viewMode < 2 && bottomButtons.Count > 0)
							{
								isNavigatingBottomButtons = true;
								selectedButtonIndex = 1;
							}
						}
						UpdateSelectionVisuals();
					}
					
					if (upPressed && !previousUpState)
					{
						if (isNavigatingBottomButtons)
						{
							isNavigatingBottomButtons = false;
							selectedTaskIndex = totalTasksCount - 1;
						}
						else if (isNavigatingHeaderButtons)
						{
							// stay on header
						}
						else if (selectedTaskIndex > 0)
						{
							selectedTaskIndex--;
						}
						else if (headerButtons.Count > 0)
						{
							isNavigatingHeaderButtons = true;
							selectedHeaderIndex = 0;
						}
						UpdateSelectionVisuals();
					}

					// Horizontal Navigation (bottom or header buttons)
					if (isNavigatingHeaderButtons)
					{
						if (leftPressed && !previousLeftState && selectedHeaderIndex > 0)
						{
							selectedHeaderIndex--;
							UpdateSelectionVisuals();
						}
						if (rightPressed && !previousRightState && selectedHeaderIndex < headerButtons.Count - 1)
						{
							selectedHeaderIndex++;
							UpdateSelectionVisuals();
						}
					}
					else if (isNavigatingBottomButtons)
					{
						if (leftPressed && !previousLeftState && selectedButtonIndex > 0)
						{
							selectedButtonIndex--;
							UpdateSelectionVisuals();
						}
						if (rightPressed && !previousRightState && selectedButtonIndex < bottomButtons.Count - 1)
						{
							selectedButtonIndex++;
							UpdateSelectionVisuals();
						}
					}

					// Action (A button)
					if (aPressed && !previousAState)
					{
						if (isNavigatingHeaderButtons)
						{
							if (selectedHeaderIndex >= 0 && selectedHeaderIndex < headerButtons.Count)
								headerButtons[selectedHeaderIndex].RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
						}
						else if (!isNavigatingBottomButtons)
						{
							if (selectedTaskIndex >= 0 && selectedTaskIndex < taskCheckBoxes.Count)
							{
								taskCheckBoxes[selectedTaskIndex].IsChecked = !taskCheckBoxes[selectedTaskIndex].IsChecked;
							}
						}
						else
						{
							if (selectedButtonIndex >= 0 && selectedButtonIndex < bottomButtons.Count)
							{
								// Programmatically click the button
								bottomButtons[selectedButtonIndex].RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));
							}
						}
					}

					previousUpState = upPressed;
					previousDownState = downPressed;
					previousLeftState = leftPressed;
					previousRightState = rightPressed;
					previousAState = aPressed;
					previousLBState = lbPressed;
					previousRBState = rbPressed;
				}
			}
			else
			{
				if (gameController != IntPtr.Zero)
				{
					SDL.SDL_GameControllerClose(gameController);
					gameController = IntPtr.Zero;
				}
			}
		}
		catch { }
	}

	private void DragHandle_MouseDown(object sender, MouseButtonEventArgs e)
	{
		if (e.ChangedButton == MouseButton.Left)
		{
			try { this.DragMove(); } catch { }
		}
	}

	private void MainWindow_Loaded(object sender, RoutedEventArgs e)
	{
		EnsureTopmost();

		topmostTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(150) };
		topmostTimer.Tick += delegate { EnsureTopmost(); };
		topmostTimer.Start();

		// Smooth fade-in entrance.
		var fadeIn = new DoubleAnimation(0.0, 1.0, TimeSpan.FromMilliseconds(350))
		{
			EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
		};
		BeginAnimation(OpacityProperty, fadeIn);
	}

	// Smoothly eases the ScrollViewer to a target offset instead of snapping.
	private DispatcherTimer _scrollAnimTimer;
	private double _scrollAnimTarget;
	private void AnimateScrollTo(double target)
	{
		if (scrollViewer == null) return;
		_scrollAnimTarget = target;
		if (_scrollAnimTimer == null)
		{
			_scrollAnimTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(15) };
			_scrollAnimTimer.Tick += delegate
			{
				double cur = scrollViewer.VerticalOffset;
				double diff = _scrollAnimTarget - cur;
				if (Math.Abs(diff) < 0.5)
				{
					scrollViewer.ScrollToVerticalOffset(_scrollAnimTarget);
					_scrollAnimTimer.Stop();
					return;
				}
				scrollViewer.ScrollToVerticalOffset(cur + diff * 0.22); // ease-out chase
			};
		}
		_scrollAnimTimer.Start();
	}

	private double GetEffectiveScrollOffset()
	{
		if (scrollViewer == null) return _scrollOffsetBeforeRebuild;
		double offset = scrollViewer.VerticalOffset;
		if (_scrollAnimTimer != null && _scrollAnimTimer.IsEnabled)
			offset = Math.Max(offset, _scrollAnimTarget);
		return offset;
	}

	private void RestoreScrollAfterRebuild()
	{
		if (scrollViewer == null) return;
		scrollViewer.ScrollToVerticalOffset(_scrollOffsetBeforeRebuild);
	}

	// Animates a progress bar filling from 0 to its value with an ease-out.
	private void AnimateBar(ProgressBar bar, double to)
	{
		if (bar == null) return;
		var anim = new DoubleAnimation(0.0, to, TimeSpan.FromMilliseconds(650))
		{
			EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
		};
		bar.BeginAnimation(System.Windows.Controls.Primitives.RangeBase.ValueProperty, anim);
	}

	private static Color Lighten(Color c, double amt)
	{
		return Color.FromRgb(
			(byte)(c.R + (255 - c.R) * amt),
			(byte)(c.G + (255 - c.G) * amt),
			(byte)(c.B + (255 - c.B) * amt));
	}

	// A flowing highlight that sweeps across the filled portion of a bar.
	// We scroll a repeating gradient via a RelativeTransform so the band loops
	// seamlessly (gradient-stop offsets are clamped to [0,1] in WPF).
	private Brush MakeShimmerBrush(Color baseColor)
	{
		Color light = Lighten(baseColor, 0.6);
		var brush = new LinearGradientBrush
		{
			StartPoint = new Point(0, 0.5),
			EndPoint = new Point(1, 0.5),
			SpreadMethod = GradientSpreadMethod.Repeat
		};
		brush.GradientStops.Add(new GradientStop(baseColor, 0.0));
		brush.GradientStops.Add(new GradientStop(light, 0.5));
		brush.GradientStops.Add(new GradientStop(baseColor, 1.0));

		var tt = new TranslateTransform(0, 0);
		brush.RelativeTransform = tt;
		var anim = new DoubleAnimation(0.0, 1.0, TimeSpan.FromSeconds(2.2)) { RepeatBehavior = RepeatBehavior.Forever };
		tt.BeginAnimation(TranslateTransform.XProperty, anim);
		return brush;
	}

	// Soft white halo that gently breathes, applied to the active selection.
	private void ApplyPulsingGlow(UIElement el)
	{
		if (el == null) return;
		var glow = new DropShadowEffect { Color = Colors.White, ShadowDepth = 0, BlurRadius = 10, Opacity = 0.4 };
		el.Effect = glow;
		var ease = new SineEase { EasingMode = EasingMode.EaseInOut };
		var blur = new DoubleAnimation(8, 20, TimeSpan.FromMilliseconds(950)) { AutoReverse = true, RepeatBehavior = RepeatBehavior.Forever, EasingFunction = ease };
		var op = new DoubleAnimation(0.35, 0.7, TimeSpan.FromMilliseconds(950)) { AutoReverse = true, RepeatBehavior = RepeatBehavior.Forever, EasingFunction = ease };
		glow.BeginAnimation(DropShadowEffect.BlurRadiusProperty, blur);
		glow.BeginAnimation(DropShadowEffect.OpacityProperty, op);
	}

	// Green pulse on a freshly completed quest row (background flashes then settles).
	private void FlashComplete(Border b, Color baseColor)
	{
		if (b == null) return;
		try
		{
			Color flash = Color.FromRgb(70, 185, 70);
			var brush = new SolidColorBrush(flash);
			b.Background = brush;
			var anim = new ColorAnimation(flash, baseColor, TimeSpan.FromMilliseconds(700))
			{
				EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
			};
			anim.Completed += delegate { b.Background = new SolidColorBrush(baseColor); };
			brush.BeginAnimation(SolidColorBrush.ColorProperty, anim);
		}
		catch { }
	}

	// Staggered fade + slide-up entrance for the task rows (first render only).
	private void PlayEntranceAnimation()
	{
		try
		{
			for (int i = 0; i < taskBorders.Count; i++)
			{
				Border b = taskBorders[i];
				var tt = new TranslateTransform(0, 14);
				b.RenderTransform = tt;
				b.Opacity = 0;
				var begin = TimeSpan.FromMilliseconds(i * 28);
				var fade = new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(280)) { BeginTime = begin, EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut } };
				var slide = new DoubleAnimation(14, 0, TimeSpan.FromMilliseconds(320)) { BeginTime = begin, EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut } };
				b.BeginAnimation(OpacityProperty, fade);
				tt.BeginAnimation(TranslateTransform.YProperty, slide);
			}
		}
		catch { }
	}

	private void EnsureTopmost()
	{
		try
		{
			IntPtr hwnd = new WindowInteropHelper(this).Handle;
			if (hwnd == IntPtr.Zero) return;
			SetWindowPos(hwnd, HWND_TOPMOST, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE);
		}
		catch { }
	}

	// Scrolls so the currently selected task is centered in the visible area,
	// regardless of how many tasks there are or their individual heights.
	private void ScrollSelectedIntoView()
	{
		try
		{
			if (scrollViewer == null || tasksPanel == null) return;
			if (selectedTaskIndex < 0 || selectedTaskIndex >= taskBorders.Count) return;
			if (scrollViewer.ViewportHeight <= 0) return;

			Border border = taskBorders[selectedTaskIndex];
			GeneralTransform transform = border.TransformToAncestor(tasksPanel);
			Point pos = transform.Transform(new Point(0, 0));
			double target = pos.Y - (scrollViewer.ViewportHeight - border.ActualHeight) / 2.0;
			if (target < 0) target = 0;
			if (target > scrollViewer.ScrollableHeight) target = scrollViewer.ScrollableHeight;
			AnimateScrollTo(target);
		}
		catch { }
	}
	private void MainWindow_Closed(object sender, EventArgs e)
	{
		SaveWindowGeometry();
		topmostTimer?.Stop();
		QuestNarrator.Dispose();
	}

	private void RestoreWindowGeometry()
	{
		try
		{
			if (Settings.Default.OverlayLeft >= 0 && Settings.Default.OverlayTop >= 0)
			{
				WindowStartupLocation = WindowStartupLocation.Manual;
				Left = Settings.Default.OverlayLeft;
				Top = Settings.Default.OverlayTop;
				if (Settings.Default.OverlayWidth >= 200) Width = Settings.Default.OverlayWidth;
				if (Settings.Default.OverlayHeight >= 160) Height = Settings.Default.OverlayHeight;
			}
		}
		catch { }
	}

	private void SaveWindowGeometry()
	{
		try
		{
			Settings.Default.OverlayLeft = Left;
			Settings.Default.OverlayTop = Top;
			Settings.Default.OverlayWidth = Width;
			Settings.Default.OverlayHeight = Height;
			Settings.Default.Save();
		}
		catch { }
	}

	private void ScheduleGeometrySave()
	{
		if (_geometrySaveTimer == null)
		{
			_geometrySaveTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(400) };
			_geometrySaveTimer.Tick += delegate
			{
				_geometrySaveTimer.Stop();
				SaveWindowGeometry();
			};
		}
		_geometrySaveTimer.Stop();
		_geometrySaveTimer.Start();
	}

	private string GetBindSummaryText()
	{
		string check = Loc.FormatKeyboardBind(Settings.Default.CheckKey, Settings.Default.CheckModifiers);
		string undo = Loc.FormatKeyboardBind(Settings.Default.UncheckKey, Settings.Default.UncheckModifiers);
		string line = Loc.BindSummaryLine(check, undo, Loc.ControllerShortName(_checkButton), Loc.ControllerShortName(_uncheckButton));
		if (Settings.Default.NarratorEnabled)
			line += " · Y";
		return line;
	}

	private string GetNarrationText()
	{
		ShowUserProgress progress = DatabaseHelper.GetUserProgress(_dbPath, _charName, _regionName);
		string region = Loc.RegionDisplayName(_regionName);
		if (progress.labels != null)
		{
			foreach (var t in progress.labels)
			{
				if (t.Item2 != 1)
					return t.Item1;
			}
		}
		return Loc.NarratorAllDone(region);
	}

	private void NarrateCurrentQuest()
	{
		if (!Settings.Default.NarratorEnabled) return;
		QuestNarrator.Speak(GetNarrationText());
	}

	private void ScheduleAutoNarrate()
	{
		if (Settings.Default.NarratorEnabled && Settings.Default.NarratorAutoRead)
			_scheduleNarrateAfterLayout = true;
	}

	private void UncheckLast(object? sender, HotkeyEventArgs e)
	{
		UncheckLastChecked();
	}

	public void CheckFirst(object? sender, HotkeyEventArgs e)
	{
		CheckLastUnchecked();
	}

	private static Key ParseKey(string s, Key fallback) => Enum.TryParse<Key>(s, out var k) ? k : fallback;
	private static ModifierKeys ParseMods(string s, ModifierKeys fallback) => Enum.TryParse<ModifierKeys>(s, out var m) ? m : fallback;

	// (Re)registers the global keyboard shortcuts for validate / undo from settings.
	private void ApplyKeyboardBinds()
	{
		try
		{
			Key checkKey = ParseKey(Settings.Default.CheckKey, Key.Down);
			ModifierKeys checkMods = ParseMods(Settings.Default.CheckModifiers, ModifierKeys.Control);
			Key uncheckKey = ParseKey(Settings.Default.UncheckKey, Key.Up);
			ModifierKeys uncheckMods = ParseMods(Settings.Default.UncheckModifiers, ModifierKeys.Control);
			HotkeyManager.Current.AddOrReplace("Check", checkKey, checkMods, CheckFirst);
			HotkeyManager.Current.AddOrReplace("Uncheck", uncheckKey, uncheckMods, UncheckLast);
		}
		catch
		{
			TrackerLog.Error("Failed to register keyboard shortcut.");
			MessageBox.Show(Loc.HotkeyRegisterFailed,
				Loc.BindTitle, MessageBoxButton.OK, MessageBoxImage.Warning);
		}
	}

	private void ApplyControllerBinds()
	{
		_checkButton = Settings.Default.CheckButton;
		_uncheckButton = Settings.Default.UncheckButton;
	}

	private void OpenBindWindow()
	{
		try
		{
			_suspendController = true;          // pause controller handling
			topmostTimer?.Stop();               // let the dialog sit above the overlay
			try
			{
				// Disable the global shortcuts while capturing, so pressing the
				// current combo doesn't actually validate/undo a quest.
				HotkeyManager.Current.Remove("Check");
				HotkeyManager.Current.Remove("Uncheck");
			}
			catch { }

			BindWindow w = new BindWindow(gameController) { Owner = this, Topmost = true };
			w.ShowDialog();

			ApplyKeyboardBinds();
			ApplyControllerBinds();
			BuildUI();
		}
		catch { }
		finally
		{
			_suspendController = false;
			topmostTimer?.Start();
			EnsureTopmost();
		}
	}

	public void NextPage(object? sender, HotkeyEventArgs e)
	{
		ChangeRegionProgress(1);
	}

	public void PreviousPage(object? sender, HotkeyEventArgs e)
	{
		ChangeRegionProgress(-1);
	}

	private void BuildUI()
	{
		if (_scrollAnimTimer != null && _scrollAnimTimer.IsEnabled)
			_scrollAnimTimer.Stop();

		// Keep scroll across rebuilds — new ScrollViewer starts at 0, don't overwrite with that.
		if (scrollViewer != null)
		{
			double offset = GetEffectiveScrollOffset();
			if (offset > 0.5)
				_scrollOffsetBeforeRebuild = offset;
		}

		RootGrid.Children.Clear();
		RootGrid.RowDefinitions.Clear();
		taskBorders.Clear();
		taskCheckBoxes.Clear();
		totalTasksCount = 0;

		RowDefinition rowHeader = new RowDefinition { Height = GridLength.Auto };
		RowDefinition rowFill = _viewMode >= 1
			? new RowDefinition { Height = GridLength.Auto }
			: new RowDefinition { Height = new GridLength(1.0, GridUnitType.Star) };
		RowDefinition rowAuto = new RowDefinition { Height = GridLength.Auto };
		RootGrid.RowDefinitions.Add(rowHeader);
		RootGrid.RowDefinitions.Add(rowFill);
		RootGrid.RowDefinitions.Add(rowAuto);

		// Fixed header (title + progress) that stays visible while tasks scroll.
		StackPanel headerPanel = new StackPanel { HorizontalAlignment = HorizontalAlignment.Stretch };

		scrollViewer = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Margin = new Thickness(5.0, 2.0, 5.0, 5.0) };
		tasksPanel = new StackPanel { HorizontalAlignment = HorizontalAlignment.Stretch };
		
		ShowUserProgress progress = DatabaseHelper.GetUserProgress(_dbPath, _charName, _regionName);

		string currentQuestLabel = null;
		if (progress.labels != null)
		{
			foreach (var t in progress.labels)
			{
				if (t.Item2 != 1) { currentQuestLabel = t.Item1; break; }
			}
		}

		// Compact header: avatar + character info (no duplicate zone title block).
		FontFamily poppinsHeader = new FontFamily(new Uri("pack://application:,,,/"), "./Fonts/#Poppins");

		Grid headerCardGrid = new Grid();
		headerCardGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(48) });
		headerCardGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

		if (AppAssets.HasHubAvatar(_charName))
		{
			Image avatarImage = new Image
			{
				Source = AppAssets.AvatarCropped,
				Width = 42,
				Height = 42,
				Stretch = Stretch.UniformToFill,
				Margin = new Thickness(0, 0, 8, 0),
				VerticalAlignment = VerticalAlignment.Top
			};
			Grid.SetColumn(avatarImage, 0);
			headerCardGrid.Children.Add(avatarImage);
		}
		else
		{
			headerCardGrid.ColumnDefinitions[0].Width = new GridLength(0);
		}

		StackPanel infoStack = new StackPanel();
		infoStack.Children.Add(new TextBlock
		{
			Text = _charName,
			FontSize = 13,
			FontWeight = FontWeights.Bold,
			Foreground = Brushes.White,
			FontFamily = poppinsHeader
		});
		infoStack.Children.Add(new TextBlock
		{
			Text = $"{Loc.RegionDisplayName(_regionName)} · {progress.Title}",
			FontSize = 10,
			Foreground = new SolidColorBrush(Color.FromArgb(0xCC, 0xDD, 0xDD, 0xDD)),
			Margin = new Thickness(0, 1, 0, 0),
			TextWrapping = TextWrapping.Wrap
		});
		if (currentQuestLabel != null)
		{
			infoStack.Children.Add(new TextBlock
			{
				Text = $"► {currentQuestLabel}",
				FontSize = 9,
				Foreground = new SolidColorBrush(Color.FromArgb(0x88, 0xDD, 0xDD, 0xDD)),
				TextWrapping = TextWrapping.Wrap,
				MaxHeight = 26,
				Margin = new Thickness(0, 2, 0, 0)
			});
		}
		Grid.SetColumn(infoStack, 1);
		headerCardGrid.Children.Add(infoStack);

		headerButtons.Clear();

		Button viewModeButton = new Button
		{
			Style = (Style)FindResource("ModernButton"),
			Content = GetViewModeButtonContent(),
			FontSize = 12,
			Width = 26.0,
			Height = 26.0,
			Padding = new Thickness(0),
			Margin = new Thickness(0, 0, 4, 0),
			ToolTip = GetViewModeToolTip()
		};
		viewModeButton.Click += delegate { CycleViewMode(); };

		Button hubButton = new Button
		{
			Style = (Style)FindResource("ModernButton"),
			Content = "⌂",
			FontSize = 12,
			Width = 26.0,
			Height = 26.0,
			Padding = new Thickness(0),
			Margin = new Thickness(0, 0, 4, 0),
			ToolTip = Loc.HubToolTip
		};
		hubButton.Click += delegate
		{
			SaveWindowGeometry();
			new LoginWindow().Show();
			Close();
		};

		Button bindButton = new Button
		{
			Style = (Style)FindResource("ModernButton"),
			Content = "⌨",
			FontSize = 13,
			Width = 30.0,
			Height = 26.0,
			Padding = new Thickness(0),
			ToolTip = Loc.BindToolTip
		};
		bindButton.Click += delegate { OpenBindWindow(); };

		headerButtons.Add(viewModeButton);
		headerButtons.Add(hubButton);
		headerButtons.Add(bindButton);

		Button narratorButton = null;
		if (Settings.Default.NarratorEnabled)
		{
			narratorButton = new Button
			{
				Style = (Style)FindResource("ModernButton"),
				Content = "🔊",
				FontSize = 12,
				Width = 26.0,
				Height = 26.0,
				Padding = new Thickness(0),
				Margin = new Thickness(0, 0, 4, 0),
				ToolTip = Loc.NarratorToolTip
			};
			narratorButton.Click += delegate
			{
				if (QuestNarrator.IsSpeaking)
					QuestNarrator.Stop();
				else
					NarrateCurrentQuest();
			};
			headerButtons.Add(narratorButton);
		}

		// Toolbar above the card — keeps toggles off the avatar.
		StackPanel headerToolbar = new StackPanel
		{
			Orientation = Orientation.Horizontal,
			HorizontalAlignment = HorizontalAlignment.Right,
			Margin = new Thickness(8, 4, 8, 2)
		};
		headerToolbar.Children.Add(viewModeButton);
		headerToolbar.Children.Add(hubButton);
		headerToolbar.Children.Add(bindButton);
		if (narratorButton != null)
			headerToolbar.Children.Add(narratorButton);
		headerPanel.Children.Add(headerToolbar);

		headerPanel.Children.Add(new Border
		{
			Background = new SolidColorBrush(Color.FromArgb(0x55, 0x11, 0x11, 0x11)),
			CornerRadius = new CornerRadius(10),
			BorderBrush = new SolidColorBrush(Color.FromArgb(0x40, 0xFF, 0xFF, 0xFF)),
			BorderThickness = new Thickness(1),
			Margin = new Thickness(8, 0, 8, 4),
			Padding = new Thickness(8, 6, 8, 6),
			Child = headerCardGrid
		});

		int zoneDone = 0;
		int zoneTotal = progress.labels?.Count ?? 0;
		if (progress.labels != null)
		{
			foreach (var t in progress.labels)
				if (t.Item2 == 1) zoneDone++;
		}
		int zoneRemaining = zoneTotal - zoneDone;
		if (zoneTotal > 0 && _viewMode == 0)
		{
			headerPanel.Children.Add(new TextBlock
			{
				Text = $"{Loc.QuestCount(zoneDone, zoneTotal)} · {Loc.QuestRemaining(zoneRemaining)}",
				FontSize = 9,
				Foreground = new SolidColorBrush(Color.FromArgb(0x88, 0xDD, 0xDD, 0xDD)),
				HorizontalAlignment = HorizontalAlignment.Center,
				Margin = new Thickness(8, 0, 8, 2)
			});
		}

		// Calculate and Display Progress
		double regionPct = DatabaseHelper.GetRegionProgressPercentage(_dbPath, _charName, _regionName);
		if (double.IsNaN(regionPct) || double.IsInfinity(regionPct)) regionPct = 0;
		double totalPct = DatabaseHelper.GetTotalProgressPercentage(_dbPath, _charName);
		if (double.IsNaN(totalPct) || double.IsInfinity(totalPct)) totalPct = 0;

		string regionDisplay = Loc.RegionDisplayName(_regionName);

		ProgressBar regionBar = null;
		ProgressBar globalBar = null;
		Grid regionGrid = null;

		if (_viewMode == 0)
		{
		StackPanel progressPanel = new StackPanel { Margin = new Thickness(8.0, 0, 8.0, 8.0) };

		// Region Progress
		regionGrid = new Grid { Margin = new Thickness(0, 0, 0, 5) };
		regionBar = new ProgressBar 
		{ 
			Value = 0,
			Height = 12, 
			Foreground = MakeShimmerBrush(Color.FromRgb(100, 200, 100)),
			Background = new SolidColorBrush(Color.FromArgb(100, 50, 50, 50)),
			BorderThickness = new Thickness(0)
		};

		TextBlock regionLabel = new TextBlock 
		{ 
			Text = $"{regionDisplay}: 0.0%", 
			Foreground = new SolidColorBrush(Colors.White), 
			FontSize = 10, 
			FontFamily = new FontFamily(new Uri("pack://application:,,,/"), "./Fonts/#Poppins"),
			FontWeight = FontWeights.Bold,
			HorizontalAlignment = HorizontalAlignment.Center,
			VerticalAlignment = VerticalAlignment.Center
		};
		regionGrid.Children.Add(regionBar);
		regionGrid.Children.Add(regionLabel);
		regionBar.ValueChanged += delegate { regionLabel.Text = $"{regionDisplay}: {regionBar.Value:F1}%"; };
		progressPanel.Children.Add(regionGrid);

		// Global Progress
		Grid globalGrid = new Grid();
		globalBar = new ProgressBar 
		{ 
			Value = 0,
			Height = 12, 
			Foreground = MakeShimmerBrush(Color.FromRgb(100, 150, 255)),
			Background = new SolidColorBrush(Color.FromArgb(100, 50, 50, 50)),
			BorderThickness = new Thickness(0)
		};

		TextBlock globalLabel = new TextBlock 
		{ 
			Text = $"{Loc.GlobalProgress}: 0.0%", 
			Foreground = new SolidColorBrush(Colors.White), 
			FontSize = 10, 
			FontFamily = new FontFamily(new Uri("pack://application:,,,/"), "./Fonts/#Poppins"),
			FontWeight = FontWeights.Bold,
			HorizontalAlignment = HorizontalAlignment.Center,
			VerticalAlignment = VerticalAlignment.Center
		};
		globalGrid.Children.Add(globalBar);
		globalGrid.Children.Add(globalLabel);
		globalBar.ValueChanged += delegate { globalLabel.Text = $"{Loc.GlobalProgress}: {globalBar.Value:F1}%"; };
		progressPanel.Children.Add(globalGrid);

		// Minimal bind reminder — low opacity, keys only.
		progressPanel.Children.Add(new TextBlock
		{
			Text = GetBindSummaryText(),
			FontSize = 9,
			Foreground = new SolidColorBrush(Color.FromArgb(0x66, 0xDD, 0xDD, 0xDD)),
			HorizontalAlignment = HorizontalAlignment.Center,
			TextAlignment = TextAlignment.Center,
			Margin = new Thickness(0, 6, 0, 0)
		});

		headerPanel.Children.Add(progressPanel);
		}
		else if (_viewMode == 1)
		{
			regionGrid = new Grid { Margin = new Thickness(8, 0, 8, 6) };
			regionBar = new ProgressBar
			{
				Value = 0,
				Height = 8,
				Foreground = MakeShimmerBrush(Color.FromRgb(100, 200, 100)),
				Background = new SolidColorBrush(Color.FromArgb(100, 50, 50, 50)),
				BorderThickness = new Thickness(0)
			};
			TextBlock compactRegionLabel = new TextBlock
			{
				Text = $"{regionDisplay}: 0.0%",
				Foreground = new SolidColorBrush(Colors.White),
				FontSize = 9,
				FontFamily = poppinsHeader,
				FontWeight = FontWeights.Bold,
				HorizontalAlignment = HorizontalAlignment.Center,
				VerticalAlignment = VerticalAlignment.Center
			};
			regionGrid.Children.Add(regionBar);
			regionGrid.Children.Add(compactRegionLabel);
			regionBar.ValueChanged += delegate { compactRegionLabel.Text = $"{regionDisplay}: {regionBar.Value:F1}%"; };
			headerPanel.Children.Add(regionGrid);
		}

		// Subtle divider under the fixed header.
		Border headerDivider = new Border
		{
			Height = 1.0,
			Margin = new Thickness(10.0, 0.0, 10.0, 0.0),
			Background = new SolidColorBrush(Color.FromArgb(40, 255, 255, 255))
		};
		headerPanel.Children.Add(headerDivider);

		Grid.SetRow(headerPanel, 0);
		RootGrid.Children.Add(headerPanel);

		string regionDb = GetRegionDb();
		if (regionDb == null) return;

		if (_viewMode >= 1)
		{
			StackPanel compactPanel = new StackPanel { Margin = new Thickness(5.0, 2.0, 5.0, 5.0) };
			if (progress.labels != null && progress.labels.Count > 0)
			{
				int pick = -1;
				for (int i = 0; i < progress.labels.Count; i++)
				{
					if (progress.labels[i].Item2 != 1) { pick = i; break; }
				}
				if (pick < 0) pick = progress.labels.Count - 1;
				selectedTaskIndex = 0; // compact UI shows a single row at index 0
				var task = progress.labels[pick];
				AddQuestRow(compactPanel, progress, regionDb, task.Item1, task.Item2, 0);
			}
			else
			{
				compactPanel.Children.Add(new TextBlock
				{
					Text = Loc.AllQuestsDone,
					Foreground = new SolidColorBrush(Color.FromArgb(0x99, 0xDD, 0xDD, 0xDD)),
					TextAlignment = TextAlignment.Center,
					HorizontalAlignment = HorizontalAlignment.Stretch,
					Margin = new Thickness(10.0, 16.0, 10.0, 16.0),
					FontSize = (int)Application.Current.Resources["FontSize"]
				});
			}
			Grid.SetRow(compactPanel, 1);
			RootGrid.Children.Add(compactPanel);
		}
		else
		{
			int index = 0;
			if (progress.labels != null)
			{
				foreach (var task in progress.labels)
				{
					AddQuestRow(tasksPanel, progress, regionDb, task.Item1, task.Item2, index);
					index++;
				}
			}
			scrollViewer.Content = tasksPanel;
			Grid.SetRow(scrollViewer, 1);
			RootGrid.Children.Add(scrollViewer);
		}
		
		if (_viewMode < 2)
		{
		Grid buttonGrid = new Grid { HorizontalAlignment = HorizontalAlignment.Stretch, Margin = new Thickness(5.0) };
		buttonGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto }); // 0: <<
		buttonGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1.0, GridUnitType.Star) });
		buttonGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto }); // 2: Switch
		buttonGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1.0, GridUnitType.Star) });
		buttonGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto }); // 4: A-
		buttonGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto }); // 5: A+
		buttonGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1.0, GridUnitType.Star) });
		buttonGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto }); // 7: FR/EN
		buttonGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1.0, GridUnitType.Star) });
		buttonGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto }); // 9: >>

		Style modernButtonStyle = (Style)FindResource("ModernButton");

		Button previousButton = new Button
		{
			Style = modernButtonStyle,
			Content = "<<", Margin = new Thickness(5.0), Width = 35.0, Height = 40.0
		};
		previousButton.Click += delegate { ChangeRegionProgress(-1); };
		Grid.SetColumn(previousButton, 0);

		Button switchCharacterButton = new Button
		{
			Style = modernButtonStyle,
			Content = "Switch", Margin = new Thickness(5.0), Width = 65.0, Height = 40.0
		};
		switchCharacterButton.Click += delegate
		{
			Settings.Default.LastUser = "";
			Settings.Default.LastRegion = "";
			Settings.Default.RememberMe = false;
			Settings.Default.Save();
			new LoginWindow().Show();
			Close();
		};
		Grid.SetColumn(switchCharacterButton, 2);
		
		// Direct Font Size Controls
		Button fontMinusBtn = new Button
		{
			Style = modernButtonStyle,
			Content = "A-", Margin = new Thickness(5.0, 5.0, 2.5, 5.0), Width = 35.0, Height = 40.0
		};
		fontMinusBtn.Click += delegate
		{
			int currentSize = (int)Application.Current.Resources["FontSize"];
			if (currentSize > 10) 
			{
				Application.Current.Resources["FontSize"] = currentSize - 1;
				Settings.Default.FontSize = currentSize - 1;
				Settings.Default.Save();
				BuildUI(); 
			}
		};
		Grid.SetColumn(fontMinusBtn, 4);

		Button fontPlusBtn = new Button
		{
			Style = modernButtonStyle,
			Content = "A+", Margin = new Thickness(2.5, 5.0, 5.0, 5.0), Width = 35.0, Height = 40.0
		};
		fontPlusBtn.Click += delegate
		{
			int currentSize = (int)Application.Current.Resources["FontSize"];
			if (currentSize < 24) 
			{
				Application.Current.Resources["FontSize"] = currentSize + 1;
				Settings.Default.FontSize = currentSize + 1;
				Settings.Default.Save();
				BuildUI(); 
			}
		};
		Grid.SetColumn(fontPlusBtn, 5);

		// FR/EN Language Toggle
		Button langButton = new Button
		{
			Style = modernButtonStyle,
			Content = AppConfig.Language == "FR" ? "FR" : "EN", Margin = new Thickness(5.0), Width = 35.0, Height = 40.0
		};
		langButton.Click += delegate
		{
			AppConfig.Language = AppConfig.Language == "FR" ? "EN" : "FR";
			Settings.Default.Language = AppConfig.Language;
			Settings.Default.Save();
			BuildUI();
		};
		Grid.SetColumn(langButton, 7);

		Button nextButton = new Button
		{
			Style = modernButtonStyle,
			Content = ">>", Margin = new Thickness(5.0), Width = 35.0, Height = 40.0
		};
		nextButton.Click += delegate { ChangeRegionProgress(1); };
		Grid.SetColumn(nextButton, 9);

		buttonGrid.Children.Add(previousButton);
		buttonGrid.Children.Add(switchCharacterButton);
		buttonGrid.Children.Add(fontMinusBtn);
		buttonGrid.Children.Add(fontPlusBtn);
		buttonGrid.Children.Add(langButton);
		buttonGrid.Children.Add(nextButton);
		Grid.SetRow(buttonGrid, 2);
		RootGrid.Children.Add(buttonGrid);

		bottomButtons.Clear();
		bottomButtons.Add(previousButton);
		bottomButtons.Add(switchCharacterButton);
		bottomButtons.Add(fontMinusBtn);
		bottomButtons.Add(fontPlusBtn);
		bottomButtons.Add(langButton);
		bottomButtons.Add(nextButton);

		} // end footer (_viewMode < 2)

		// Restore state
		UpdateSelectionVisuals();

		int layoutToken = ++_scrollUiToken;
		bool recenterAfterLayout = _recenterOnRebuild;
		if (recenterAfterLayout) _recenterOnRebuild = false;

		// We must wait for the UI to be fully rendered before we can scroll
		Dispatcher.BeginInvoke(new Action(() => 
		{
			if (layoutToken != _scrollUiToken) return;

			if (regionBar != null) AnimateBar(regionBar, regionPct);
			if (globalBar != null) AnimateBar(globalBar, totalPct);

			if (regionGrid != null)
			{
				if (_pulseRegionBar)
				{
					_pulseRegionBar = false;
					PulseRegionComplete(regionGrid);
				}
				else if (regionPct >= 99.95)
					PulseRegionComplete(regionGrid);
			}

			if (_firstBuild)
			{
				_firstBuild = false;
				PlayEntranceAnimation();
			}

			if (_flashTargetBorder != null)
			{
				FlashComplete(_flashTargetBorder, _flashBaseColor);
			}
			_flashTargetBorder = null;
			_flashLabel = null;

			if (recenterAfterLayout)
			{
				RestoreScrollAfterRebuild();
				ScrollToCurrentQuest();
			}
			else if (scrollViewer != null)
			{
				RestoreScrollAfterRebuild();
			}

			if (_scheduleNarrateAfterLayout)
			{
				_scheduleNarrateAfterLayout = false;
				NarrateCurrentQuest();
			}
		}), DispatcherPriority.Loaded);
	}

	private void AddQuestRow(
		Panel parent,
		ShowUserProgress progress,
		string regionDb,
		string label,
		int isDone,
		int index)
	{
		Border taskBorder = new Border
		{
			Padding = new Thickness(10.0, 12.0, 10.0, 12.0),
			Margin = new Thickness(4.0, 6.0, 4.0, 6.0),
			CornerRadius = new CornerRadius(8.0),
			BorderThickness = new Thickness(0),
			Background = ((index % 2 == 0) ? ((Brush)Application.Current.Resources["TaskEvenBackground"]) :
				((Brush)Application.Current.Resources["TaskOddBackground"]))
		};
		Grid horizontalGrid = new Grid { HorizontalAlignment = HorizontalAlignment.Stretch };
		horizontalGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
		horizontalGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1.0, GridUnitType.Star) });

		CheckBox taskCheckBox = new CheckBox
		{
			Style = (Style)FindResource("ModernCheck"),
			IsChecked = (isDone == 1),
			VerticalAlignment = VerticalAlignment.Center,
			Margin = new Thickness(2.0, 0.0, 2.0, 0.0)
		};
		taskCheckBox.Checked += delegate
		{
			ApplyQuestStatusChange(label, true, true);
		};
		taskCheckBox.Unchecked += delegate
		{
			ApplyQuestStatusChange(label, false, false);
		};
		Grid.SetColumn(taskCheckBox, 0);
		horizontalGrid.Children.Add(taskCheckBox);

		TextBlock taskText = new TextBlock
		{
			Foreground = ((index % 2 == 0) ? ((Brush)Application.Current.Resources["TaskEvenText"]) : ((Brush)Application.Current.Resources["TaskOddText"])),
			Text = label,
			FontSize = (int)Application.Current.Resources["FontSize"],
			VerticalAlignment = VerticalAlignment.Center,
			TextWrapping = TextWrapping.Wrap,
			HorizontalAlignment = HorizontalAlignment.Stretch,
			Margin = new Thickness(5.0, 0.0, 0.0, 0.0),
			Opacity = (isDone == 1) ? 0.4 : 1.0,
			TextDecorations = (isDone == 1) ? TextDecorations.Strikethrough : null
		};
		Grid.SetColumn(taskText, 1);
		horizontalGrid.Children.Add(taskText);
		taskBorder.Child = horizontalGrid;

		parent.Children.Add(taskBorder);
		taskBorders.Add(taskBorder);
		taskCheckBoxes.Add(taskCheckBox);

		if (_flashLabel != null && label == _flashLabel)
		{
			_flashTargetBorder = taskBorder;
			_flashBaseColor = (taskBorder.Background as SolidColorBrush)?.Color ?? Colors.Transparent;
		}

		totalTasksCount++;
	}

	private void OnStateChanged(object sender, EventArgs e)
	{
		if (base.WindowState == WindowState.Minimized) base.WindowState = WindowState.Normal;
	}

	private void ChangeRegionProgress(int delta)
	{
		int newRegionId = DatabaseHelper.GetUserProgress(_dbPath, _charName, _regionName).RegionId + delta;
		int maxRegionId = DatabaseHelper.GetMaxRegionId(_dbPath, _regionName);
		if (newRegionId < 1) newRegionId = 1;
		else if (newRegionId > maxRegionId) newRegionId = maxRegionId;
		
		string regionColumn = GetRegionDb();
		if (regionColumn == null) return;
		
		using (SQLiteConnection connection = new SQLiteConnection("Data Source=" + _dbPath + ";Version=3;"))
		{
			connection.Open();
			using SQLiteCommand command = new SQLiteCommand("UPDATE UserClass SET " + regionColumn + " = @newRegionId WHERE Name = @charName;", connection);
			command.Parameters.AddWithValue("@newRegionId", newRegionId);
			command.Parameters.AddWithValue("@charName", _charName);
			command.ExecuteNonQuery();
		}
		ShowUserProgress progress = DatabaseHelper.GetUserProgress(_dbPath, _charName, _regionName);
		TrackerLog.ZoneChange(_charName, _regionName, progress.RegionId, progress.Title);
		CharacterPrefs.SetLastRegion(_charName, _regionName);
		ScheduleAutoNarrate();
		BuildUI();
	}

	private string GetRegionDb()
	{
		switch (_regionName)
		{
			case "Kanto": return "KantoProgress";
			case "Johto": return "JohtoProgress";
			case "Hoenn": return "HoennProgress";
			case "Sinnoh": return "SinnohProgress";
			case "Unova": return "UnovaProgress";
			default: return null;
		}
	}

	private void CycleViewMode()
	{
		_viewMode = (_viewMode + 1) % 3;
		Settings.Default.ViewMode = _viewMode;
		Settings.Default.Save();
		TrackerLog.Info($"View mode -> {_viewMode}");
		BuildUI();
	}

	private string GetViewModeButtonContent()
	{
		switch (_viewMode)
		{
			case 1: return "▤";
			case 2: return "▢";
			default: return "▭";
		}
	}

	private string GetViewModeToolTip()
	{
		switch (_viewMode)
		{
			case 1: return Loc.MinimalModeToolTip;
			case 2: return Loc.FullModeToolTip;
			default: return Loc.CompactModeToolTip;
		}
	}

	private void PulseRegionComplete(UIElement target)
	{
		try
		{
			if (target == null) return;
			var scale = new ScaleTransform(1.0, 1.0, 0.5, 0.5);
			target.RenderTransform = scale;
			target.RenderTransformOrigin = new Point(0.5, 0.5);
			var ease = new SineEase { EasingMode = EasingMode.EaseInOut };
			var animX = new DoubleAnimation(1.0, 1.1, TimeSpan.FromMilliseconds(260))
			{
				AutoReverse = true,
				RepeatBehavior = new RepeatBehavior(2),
				EasingFunction = ease
			};
			var animY = animX.Clone();
			scale.BeginAnimation(ScaleTransform.ScaleXProperty, animX);
			scale.BeginAnimation(ScaleTransform.ScaleYProperty, animY);
		}
		catch { }
	}

	// Updates quest status via the DB (works in compact/minimal where only one row is visible).
	private void ApplyQuestStatusChange(string label, bool done, bool celebrate)
	{
		string regionDb = GetRegionDb();
		if (regionDb == null) return;

		ShowUserProgress progress = DatabaseHelper.GetUserProgress(_dbPath, _charName, _regionName);
		if (celebrate && done) _flashLabel = label;

		TrackerLog.QuestChange(_charName, _regionName, label, done);

		DatabaseHelper.UpdateTaskStatus(_dbPath, label, done ? 1 : 0, progress.RegionId, _charName, _regionName, regionDb);
		if (DatabaseHelper.CheckAndAdvanceProgress(_dbPath, _charName, _regionName, progress.RegionId, regionDb))
		{
			selectedTaskIndex = 0;
			isNavigatingBottomButtons = false;
			isNavigatingHeaderButtons = false;
		}

		ShowUserProgress updated = DatabaseHelper.GetUserProgress(_dbPath, _charName, _regionName);
		bool zoneComplete = false;
		if (updated.labels != null)
		{
			zoneComplete = true;
			foreach (var t in updated.labels)
				if (t.Item2 != 1) { zoneComplete = false; break; }
		}
		double regionPctNow = DatabaseHelper.GetRegionProgressPercentage(_dbPath, _charName, _regionName);
		_pulseRegionBar = regionPctNow >= 99.95 || (done && zoneComplete);

		double scrollOffset = GetEffectiveScrollOffset();
		if (scrollOffset > 0.5)
			_scrollOffsetBeforeRebuild = scrollOffset;

		_recenterOnRebuild = true;
		ScheduleAutoNarrate();
		BuildUI();
	}

	// Checks the first not-yet-done quest (validates the "next" quest).
	private void CheckLastUnchecked()
	{
		ShowUserProgress progress = DatabaseHelper.GetUserProgress(_dbPath, _charName, _regionName);
		if (progress.labels == null) return;

		foreach (var task in progress.labels)
		{
			if (task.Item2 != 1)
			{
				ApplyQuestStatusChange(task.Item1, true, true);
				return;
			}
		}
	}

	// Un-validates the last completed quest (undo).
	private void UncheckLastChecked()
	{
		ShowUserProgress progress = DatabaseHelper.GetUserProgress(_dbPath, _charName, _regionName);
		if (progress.labels == null) return;

		for (int i = progress.labels.Count - 1; i >= 0; i--)
		{
			if (progress.labels[i].Item2 == 1)
			{
				ApplyQuestStatusChange(progress.labels[i].Item1, false, false);
				return;
			}
		}
	}

	// Scrolls so the next quest to do (first not-done) is centered, keeping the
	// just-validated quest visible above it and upcoming quests below.
	private void ScrollToCurrentQuest()
	{
		try
		{
			if (scrollViewer == null || tasksPanel == null) return;
			if (scrollViewer.ViewportHeight <= 0) return;

			int idx = -1;
			for (int i = 0; i < taskCheckBoxes.Count; i++)
			{
				if (taskCheckBoxes[i].IsChecked != true) { idx = i; break; }
			}
			if (idx < 0) idx = taskBorders.Count - 1;
			if (idx < 0 || idx >= taskBorders.Count) return;

			Border border = taskBorders[idx];
			GeneralTransform transform = border.TransformToAncestor(tasksPanel);
			Point pos = transform.Transform(new Point(0, 0));
			double target = pos.Y - (scrollViewer.ViewportHeight - border.ActualHeight) / 2.0;
			if (target < 0) target = 0;
			if (target > scrollViewer.ScrollableHeight) target = scrollViewer.ScrollableHeight;
			AnimateScrollTo(target);
		}
		catch { }
	}
}
