using System;
using System.Windows;
using System.Windows.Markup;
using System.Windows.Media;
using PokeMMOTracker.Properties;

namespace PokeMMOTracker.Views;

public partial class SettingsWindow : Window, IComponentConnector
{
	public SettingsWindow()
	{
		InitializeComponent();
		BackgroundColorPicker.SelectedColor = ((SolidColorBrush)Application.Current.Resources["Background"]).Color;
		ButtonBackgroundColorPicker.SelectedColor = ((SolidColorBrush)Application.Current.Resources["ButtonBackground"]).Color;
		ButtonTextColorPicker.SelectedColor = ((SolidColorBrush)Application.Current.Resources["ButtonText"]).Color;
		TaskOddBackgroundColorPicker.SelectedColor = ((SolidColorBrush)Application.Current.Resources["TaskOddBackground"]).Color;
		TaskOddTextColorPicker.SelectedColor = ((SolidColorBrush)Application.Current.Resources["TaskOddText"]).Color;
		TaskEvenBackgroundColorPicker.SelectedColor = ((SolidColorBrush)Application.Current.Resources["TaskEvenBackground"]).Color;
		TaskEvenTextColorPicker.SelectedColor = ((SolidColorBrush)Application.Current.Resources["TaskEvenText"]).Color;
		CheckBoxBorderColorPicker.SelectedColor = ((SolidColorBrush)Application.Current.Resources["CheckBoxBorder"]).Color;
		FontSizeSlider.Value = Settings.Default.FontSize;
		FontSizeLbl.Content = "Font size: " + Settings.Default.FontSize;
	}

	private void UpdateResource(string key, Color? newColor)
	{
		if (newColor.HasValue)
		{
			Application.Current.Resources[key] = new SolidColorBrush(newColor.Value);
		}
	}

	private void UpdateResource(string key, int newSize)
	{
		Application.Current.Resources[key] = newSize;
	}

	private void BackgroundColorPicker_SelectedColorChanged(object sender, RoutedPropertyChangedEventArgs<Color?> e)
	{
		UpdateResource("Background", e.NewValue);
	}

	private void ButtonBackgroundColorPicker_SelectedColorChanged(object sender, RoutedPropertyChangedEventArgs<Color?> e)
	{
		UpdateResource("ButtonBackground", e.NewValue);
	}

	private void ButtonTextColorPicker_SelectedColorChanged(object sender, RoutedPropertyChangedEventArgs<Color?> e)
	{
		UpdateResource("ButtonText", e.NewValue);
	}

	private void TaskOddBackgroundColorPicker_SelectedColorChanged(object sender, RoutedPropertyChangedEventArgs<Color?> e)
	{
		UpdateResource("TaskOddBackground", e.NewValue);
	}

	private void TaskOddTextColorPicker_SelectedColorChanged(object sender, RoutedPropertyChangedEventArgs<Color?> e)
	{
		UpdateResource("TaskOddText", e.NewValue);
	}

	private void TaskEvenBackgroundColorPicker_SelectedColorChanged(object sender, RoutedPropertyChangedEventArgs<Color?> e)
	{
		UpdateResource("TaskEvenBackground", e.NewValue);
	}

	private void TaskEvenTextColorPicker_SelectedColorChanged(object sender, RoutedPropertyChangedEventArgs<Color?> e)
	{
		UpdateResource("TaskEvenText", e.NewValue);
	}

	private void CheckBoxBorderColorPicker_SelectedColorChanged(object sender, RoutedPropertyChangedEventArgs<Color?> e)
	{
		UpdateResource("CheckBoxBorder", e.NewValue);
	}

	private void SaveSettings_Click(object sender, RoutedEventArgs e)
	{
		Settings.Default.Background = ((SolidColorBrush)Application.Current.Resources["Background"]).Color.ToString();
		Settings.Default.ButtonBackground = ((SolidColorBrush)Application.Current.Resources["ButtonBackground"]).Color.ToString();
		Settings.Default.ButtonText = ((SolidColorBrush)Application.Current.Resources["ButtonText"]).Color.ToString();
		Settings.Default.TaskOddBackground = ((SolidColorBrush)Application.Current.Resources["TaskOddBackground"]).Color.ToString();
		Settings.Default.TaskOddText = ((SolidColorBrush)Application.Current.Resources["TaskOddText"]).Color.ToString();
		Settings.Default.TaskEvenBackground = ((SolidColorBrush)Application.Current.Resources["TaskEvenBackground"]).Color.ToString();
		Settings.Default.TaskEvenText = ((SolidColorBrush)Application.Current.Resources["TaskEvenText"]).Color.ToString();
		Settings.Default.CheckBoxBorder = ((SolidColorBrush)Application.Current.Resources["CheckBoxBorder"]).Color.ToString();
		Settings.Default.FontSize = (int)Application.Current.Resources["FontSize"];
		Settings.Default.Save();
		MessageBox.Show("Settings saved!");
	}

	private void FontSizeSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
	{
		UpdateResource("FontSize", Convert.ToInt32(e.NewValue));
		if (FontSizeLbl != null)
		{
			FontSizeLbl.Content = "Font size: " + Convert.ToInt32(e.NewValue);
		}
	}
}
