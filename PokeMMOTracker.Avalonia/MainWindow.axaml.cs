using System.Runtime.InteropServices;
using Avalonia.Controls;

namespace PokeMMOTracker.Avalonia;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        PlatformInfo.Text =
            $"OS: {RuntimeInformation.OSDescription}\n" +
            $"Arch: {RuntimeInformation.OSArchitecture}\n" +
            $"Runtime: {RuntimeInformation.FrameworkDescription}";
    }
}
