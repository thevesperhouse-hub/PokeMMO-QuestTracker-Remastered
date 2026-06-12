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
using System.Windows.Threading;
using NHotkey;
using NHotkey.Wpf;
using PokeMMOTracker.Models;
using PokeMMOTracker.Properties;
using SDL2;

namespace PokeMMOTracker.Views;

public partial class MainWindow : Window, IComponentConnector
{
	[DllImport("user32.dll")]
	[return: MarshalAs(UnmanagedType.Bool)]
	static extern bool SetForegroundWindow(IntPtr hWnd);

	private readonly string _dbPath;
	private readonly string _charName;
	private readonly string _regionName;

	private StackPanel tasksPanel;
	private ScrollViewer scrollViewer;

	private static readonly KeyGesture CheckGesture = new KeyGesture(Key.Down, ModifierKeys.Control);
	private static readonly KeyGesture UncheckGesture = new KeyGesture(Key.Up, ModifierKeys.Control);
	private static readonly KeyGesture PreviousGesture = new KeyGesture(Key.Left, ModifierKeys.Control);
	private static readonly KeyGesture NextGesture = new KeyGesture(Key.Right, ModifierKeys.Control);

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
	private bool previousUpState = false;
	private bool previousDownState = false;
	private bool previousLeftState = false;
	private bool previousRightState = false;

	public MainWindow(string charName, string regionName)
	{
		HotkeyManager.Current.AddOrReplace("Check", CheckGesture, CheckFirst);
		HotkeyManager.Current.AddOrReplace("Uncheck", UncheckGesture, UncheckLast);
		HotkeyManager.Current.AddOrReplace("Previous", PreviousGesture, PreviousPage);
		HotkeyManager.Current.AddOrReplace("Next", NextGesture, NextPage);
		InitializeComponent();
		base.DataContext = this;
		base.Topmost = true;
		base.StateChanged += OnStateChanged;
		base.Loaded += MainWindow_Loaded;
		base.Closed += MainWindow_Closed;
		this.WindowStartupLocation = WindowStartupLocation.CenterScreen;
		
		_charName = charName;
		_regionName = regionName;
		_dbPath = DatabaseHelper.GetDatabasePath();
		base.Width = 450.0;
		base.Height = 600.0;
		BuildUI();

		try 
		{
			SDL.SDL_Init(SDL.SDL_INIT_GAMECONTROLLER);
			controllerTimer = new DispatcherTimer();
			controllerTimer.Interval = TimeSpan.FromMilliseconds(32); 
			controllerTimer.Tick += ControllerTimer_Tick;
			controllerTimer.Start();
		}
		catch { }
	}

	private void FocusPokeMMO()
	{
		foreach (Process p in Process.GetProcesses())
		{
			if (!string.IsNullOrEmpty(p.MainWindowTitle) && p.MainWindowTitle.Contains("PokeMMO", StringComparison.OrdinalIgnoreCase))
			{
				if (p.MainWindowHandle != IntPtr.Zero)
				{
					SetForegroundWindow(p.MainWindowHandle);
					break;
				}
			}
		}
	}

	private void FocusTracker()
	{
		WindowInteropHelper helper = new WindowInteropHelper(this);
		SetForegroundWindow(helper.Handle);
		this.Activate();
		this.Focus();
	}

	private void UpdateSelectionVisuals()
	{
		// Reset everything
		for (int i = 0; i < taskBorders.Count; i++)
		{
			taskBorders[i].BorderThickness = new Thickness(0);
			taskBorders[i].BorderBrush = new SolidColorBrush(Colors.Transparent);
		}
		for (int i = 0; i < bottomButtons.Count; i++)
		{
			bottomButtons[i].BorderThickness = new Thickness(1);
			bottomButtons[i].BorderBrush = new SolidColorBrush(Color.FromRgb(85, 85, 85)); // Default gray border
		}

		if (!isTrackerModeActive) return;

		if (!isNavigatingBottomButtons)
		{
			if (selectedTaskIndex >= 0 && selectedTaskIndex < taskBorders.Count)
			{
				taskBorders[selectedTaskIndex].BorderThickness = new Thickness(2);
				taskBorders[selectedTaskIndex].BorderBrush = new SolidColorBrush(Colors.White);
				scrollViewer.ScrollToVerticalOffset(selectedTaskIndex * 50); 
			}
		}
		else
		{
			if (selectedButtonIndex >= 0 && selectedButtonIndex < bottomButtons.Count)
			{
				bottomButtons[selectedButtonIndex].BorderThickness = new Thickness(2);
				bottomButtons[selectedButtonIndex].BorderBrush = new SolidColorBrush(Colors.White);
				scrollViewer.ScrollToBottom(); // Ensure buttons are visible
			}
		}
	}

	private void ControllerTimer_Tick(object sender, EventArgs e)
	{
		try
		{
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
					isTrackerModeActive = !isTrackerModeActive;
					if (isTrackerModeActive)
					{
						System.Media.SystemSounds.Beep.Play(); 
						FocusTracker();
					}
					else
					{
						System.Media.SystemSounds.Exclamation.Play(); 
						FocusPokeMMO();
					}
					UpdateSelectionVisuals();
				}
				previousL3R3State = currentL3R3State;

				if (isTrackerModeActive)
				{
					bool upPressed = SDL.SDL_GameControllerGetButton(gameController, SDL.SDL_GameControllerButton.SDL_CONTROLLER_BUTTON_DPAD_UP) == 1;
					bool downPressed = SDL.SDL_GameControllerGetButton(gameController, SDL.SDL_GameControllerButton.SDL_CONTROLLER_BUTTON_DPAD_DOWN) == 1;
					bool leftPressed = SDL.SDL_GameControllerGetButton(gameController, SDL.SDL_GameControllerButton.SDL_CONTROLLER_BUTTON_DPAD_LEFT) == 1;
					bool rightPressed = SDL.SDL_GameControllerGetButton(gameController, SDL.SDL_GameControllerButton.SDL_CONTROLLER_BUTTON_DPAD_RIGHT) == 1;
					bool aPressed = SDL.SDL_GameControllerGetButton(gameController, SDL.SDL_GameControllerButton.SDL_CONTROLLER_BUTTON_A) == 1; 

					// Vertical Navigation
					if (downPressed && !previousDownState)
					{
						if (!isNavigatingBottomButtons)
						{
							if (selectedTaskIndex < totalTasksCount - 1) 
							{
								selectedTaskIndex++;
							}
							else 
							{
								// Move to bottom buttons
								isNavigatingBottomButtons = true;
								selectedButtonIndex = 1; // Default to middle "Switch" button
							}
						}
						UpdateSelectionVisuals();
					}
					
					if (upPressed && !previousUpState)
					{
						if (isNavigatingBottomButtons)
						{
							// Move back up to tasks
							isNavigatingBottomButtons = false;
							selectedTaskIndex = totalTasksCount - 1;
						}
						else if (selectedTaskIndex > 0)
						{
							selectedTaskIndex--;
						}
						UpdateSelectionVisuals();
					}

					// Horizontal Navigation (Only for bottom buttons)
					if (isNavigatingBottomButtons)
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
						if (!isNavigatingBottomButtons)
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
			this.DragMove();
		}
	}

	private void MainWindow_Loaded(object sender, RoutedEventArgs e) { }
	private void MainWindow_Closed(object sender, EventArgs e) { }

	private void UncheckLast(object? sender, HotkeyEventArgs e)
	{
		UncheckLastChecked();
	}

	public void CheckFirst(object? sender, HotkeyEventArgs e)
	{
		CheckLastUnchecked();
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
		// Cache current state to prevent jumping on rebuild
		double currentScrollOffset = scrollViewer?.VerticalOffset ?? 0;

		RootGrid.Children.Clear();
		RootGrid.RowDefinitions.Clear();
		taskBorders.Clear();
		taskCheckBoxes.Clear();
		totalTasksCount = 0;

		RowDefinition rowFill = new RowDefinition { Height = new GridLength(1.0, GridUnitType.Star) };
		RowDefinition rowAuto = new RowDefinition { Height = GridLength.Auto };
		RootGrid.RowDefinitions.Add(rowFill);
		RootGrid.RowDefinitions.Add(rowAuto);
		
		scrollViewer = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Margin = new Thickness(5.0) };
		tasksPanel = new StackPanel { HorizontalAlignment = HorizontalAlignment.Stretch };
		
		ShowUserProgress progress = DatabaseHelper.GetUserProgress(_dbPath, _charName, _regionName);
		
		TextBlock titleText = new TextBlock
		{
			Foreground = new SolidColorBrush(Colors.White),
			Text = progress.Title,
			FontFamily = new FontFamily(new Uri("pack://application:,,,/"), "./Fonts/#Poppins"),
			FontWeight = FontWeights.Bold,
			FontSize = (int)Application.Current.Resources["FontSize"] + 4,
			TextWrapping = TextWrapping.Wrap,
			HorizontalAlignment = HorizontalAlignment.Stretch,
			Margin = new Thickness(10.0, 15.0, 10.0, 5.0)
		};
		tasksPanel.Children.Add(titleText);

		// Calculate and Display Progress
		double regionPct = DatabaseHelper.GetRegionProgressPercentage(_dbPath, _charName, _regionName);
		double totalPct = DatabaseHelper.GetTotalProgressPercentage(_dbPath, _charName);

		StackPanel progressPanel = new StackPanel { Margin = new Thickness(10.0, 0, 10.0, 15.0) };

		// Region Progress
		Grid regionGrid = new Grid { Margin = new Thickness(0, 0, 0, 5) };
		ProgressBar regionBar = new ProgressBar 
		{ 
			Value = regionPct, 
			Height = 12, 
			Foreground = new SolidColorBrush(Color.FromRgb(100, 200, 100)),
			Background = new SolidColorBrush(Color.FromArgb(100, 50, 50, 50)),
			BorderThickness = new Thickness(0)
		};
		TextBlock regionLabel = new TextBlock 
		{ 
			Text = $"{_regionName}: {regionPct:F1}%", 
			Foreground = new SolidColorBrush(Colors.White), 
			FontSize = 10, 
			FontFamily = new FontFamily(new Uri("pack://application:,,,/"), "./Fonts/#Poppins"),
			FontWeight = FontWeights.Bold,
			HorizontalAlignment = HorizontalAlignment.Center,
			VerticalAlignment = VerticalAlignment.Center
		};
		regionGrid.Children.Add(regionBar);
		regionGrid.Children.Add(regionLabel);
		progressPanel.Children.Add(regionGrid);

		// Global Progress
		Grid globalGrid = new Grid();
		ProgressBar globalBar = new ProgressBar 
		{ 
			Value = totalPct, 
			Height = 12, 
			Foreground = new SolidColorBrush(Color.FromRgb(100, 150, 255)),
			Background = new SolidColorBrush(Color.FromArgb(100, 50, 50, 50)),
			BorderThickness = new Thickness(0)
		};
		TextBlock globalLabel = new TextBlock 
		{ 
			Text = $"Global: {totalPct:F1}%", 
			Foreground = new SolidColorBrush(Colors.White), 
			FontSize = 10, 
			FontFamily = new FontFamily(new Uri("pack://application:,,,/"), "./Fonts/#Poppins"),
			FontWeight = FontWeights.Bold,
			HorizontalAlignment = HorizontalAlignment.Center,
			VerticalAlignment = VerticalAlignment.Center
		};
		globalGrid.Children.Add(globalBar);
		globalGrid.Children.Add(globalLabel);
		progressPanel.Children.Add(globalGrid);

		tasksPanel.Children.Add(progressPanel);

		string regionDb = GetRegionDb();
		if (regionDb == null) return;

		int index = 0;
		foreach (var task in progress.labels)
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
				BorderBrush = (Brush)Application.Current.Resources["CheckBoxBorder"],
				IsChecked = (task.isDone == 1),
				VerticalAlignment = VerticalAlignment.Center
			};
			taskCheckBox.Checked += delegate
			{
				DatabaseHelper.UpdateTaskStatus(_dbPath, task.label, 1, progress.RegionId, _charName, _regionName, regionDb);
				if (DatabaseHelper.CheckAndAdvanceProgress(_dbPath, _charName, _regionName, progress.RegionId, regionDb))
				{
					selectedTaskIndex = 0;
					isNavigatingBottomButtons = false;
				}
				BuildUI(); 
			};
			taskCheckBox.Unchecked += delegate
			{
				DatabaseHelper.UpdateTaskStatus(_dbPath, task.label, 0, progress.RegionId, _charName, _regionName, regionDb);
				if (DatabaseHelper.CheckAndAdvanceProgress(_dbPath, _charName, _regionName, progress.RegionId, regionDb))
				{
					selectedTaskIndex = 0;
					isNavigatingBottomButtons = false;
				}
				BuildUI(); 
			};
			Grid.SetColumn(taskCheckBox, 0);
			horizontalGrid.Children.Add(taskCheckBox);
			
			TextBlock taskText = new TextBlock
			{
				Foreground = ((index % 2 == 0) ? ((Brush)Application.Current.Resources["TaskEvenText"]) : ((Brush)Application.Current.Resources["TaskOddText"])),
				Text = task.label,
				FontSize = (int)Application.Current.Resources["FontSize"],
				VerticalAlignment = VerticalAlignment.Center,
				TextWrapping = TextWrapping.Wrap,
				HorizontalAlignment = HorizontalAlignment.Stretch,
				Margin = new Thickness(5.0, 0.0, 0.0, 0.0)
			};
			Grid.SetColumn(taskText, 1);
			horizontalGrid.Children.Add(taskText);
			taskBorder.Child = horizontalGrid;
			
			tasksPanel.Children.Add(taskBorder);
			taskBorders.Add(taskBorder);
			taskCheckBoxes.Add(taskCheckBox);
			totalTasksCount++;
			index++;
		}
		scrollViewer.Content = tasksPanel;
		Grid.SetRow(scrollViewer, 0);
		RootGrid.Children.Add(scrollViewer);
		
		Grid buttonGrid = new Grid { HorizontalAlignment = HorizontalAlignment.Stretch, Margin = new Thickness(5.0) };
		buttonGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
		buttonGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1.0, GridUnitType.Star) });
		buttonGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
		buttonGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1.0, GridUnitType.Star) });
		buttonGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
		
		Button previousButton = new Button
		{
			Background = (Brush)Application.Current.Resources["ButtonBackground"],
			Foreground = (Brush)Application.Current.Resources["ButtonText"],
			BorderBrush = (Brush)Application.Current.Resources["ButtonText"],
			Content = "<<", Margin = new Thickness(5.0), Width = 70.0, Height = 40.0
		};
		previousButton.Click += delegate { ChangeRegionProgress(-1); };
		Grid.SetColumn(previousButton, 0);
		
		Button switchCharacterButton = new Button
		{
			Background = (Brush)Application.Current.Resources["ButtonBackground"],
			Foreground = (Brush)Application.Current.Resources["ButtonText"],
			BorderBrush = (Brush)Application.Current.Resources["ButtonText"],
			Content = "Switch", Margin = new Thickness(5.0), Width = 120.0, Height = 40.0
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
		
		Button nextButton = new Button
		{
			Background = (Brush)Application.Current.Resources["ButtonBackground"],
			Foreground = (Brush)Application.Current.Resources["ButtonText"],
			BorderBrush = (Brush)Application.Current.Resources["ButtonText"],
			Content = ">>", Margin = new Thickness(5.0), Width = 70.0, Height = 40.0
		};
		nextButton.Click += delegate { ChangeRegionProgress(1); };
		Grid.SetColumn(nextButton, 4);
		
		buttonGrid.Children.Add(previousButton);
		buttonGrid.Children.Add(switchCharacterButton);
		buttonGrid.Children.Add(nextButton);
		Grid.SetRow(buttonGrid, 1);
		RootGrid.Children.Add(buttonGrid);

		bottomButtons.Clear();
		bottomButtons.Add(previousButton);
		bottomButtons.Add(switchCharacterButton);
		bottomButtons.Add(nextButton);

		// Restore state
		UpdateSelectionVisuals();

		// We must wait for the UI to be fully rendered before we can scroll
		Dispatcher.BeginInvoke(new Action(() => 
		{
			scrollViewer.ScrollToVerticalOffset(currentScrollOffset);
		}), DispatcherPriority.Loaded);
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

	private void UncheckLastChecked() { }
	private void CheckLastUnchecked() { }
}
