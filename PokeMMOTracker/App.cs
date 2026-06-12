using System;
using System.Windows;
using System.Windows.Media;
using PokeMMOTracker.Properties;

namespace PokeMMOTracker;

public partial class App : Application
{
	protected override void OnStartup(StartupEventArgs e)
	{
	        base.OnStartup(e);
	        DatabaseHelper.EnsureDatabaseExists();
	        // Forced Dark Theme: Ignoring user settings to rely on theme.xaml
	        UpdateResourceFromSetting("FontSize", Settings.Default.FontSize);
	}

	private void UpdateResourceFromSetting(string key, string hexColor)
	{
		try
		{
			Color color = (Color)ColorConverter.ConvertFromString(hexColor);
			Application.Current.Resources[key] = new SolidColorBrush(color);
		}
		catch (Exception ex)
		{
			Console.WriteLine("Error loading setting for " + key + ": " + ex.Message);
		}
	}

	private void UpdateResourceFromSetting(string key, int fontSize)
	{
		try
		{
			Application.Current.Resources[key] = fontSize;
		}
		catch (Exception ex)
		{
			Console.WriteLine("Error loading setting for " + key + ": " + ex.Message);
		}
	}
}
