using System;
using System.Windows;
using System.Windows.Media;
using System.IO;
using System.Threading.Tasks;
using PokeMMOTracker.Properties;

namespace PokeMMOTracker;

public static class AppConfig
{
    public static string Language = "EN"; // Default to English
}

public partial class App : Application
{
        protected override void OnStartup(StartupEventArgs e)
        {
                // Global Exception Handlers (before anything that can throw)
                AppDomain.CurrentDomain.UnhandledException += (s, args) => LogCrash(args.ExceptionObject as Exception);
                DispatcherUnhandledException += (s, args) => { LogCrash(args.Exception); args.Handled = true; };
                TaskScheduler.UnobservedTaskException += (s, args) => LogCrash(args.Exception);

                try
                {
                        NativeBootstrap.EnsureSdl2();
                }
                catch (Exception ex)
                {
                        LogCrash(ex);
                        return;
                }

                base.OnStartup(e);

                try 
                {
                    DatabaseHelper.EnsureDatabaseExists();
                    LoadLanguage();
                    // Forced Dark Theme: Ignoring user settings to rely on theme.xaml
                    UpdateResourceFromSetting("FontSize", Settings.Default.FontSize);
                }
                catch (Exception ex)
                {
                    LogCrash(ex);
                }
        }

        private void LogCrash(Exception ex)
        {
            try
            {
                string crashPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "CRASH_LOG.txt");
                File.WriteAllText(crashPath, "CRASH OCCURRED:\n" + ex?.ToString());
                MessageBox.Show("A fatal error occurred. Check CRASH_LOG.txt");
                Environment.Exit(1);
            }
            catch {}
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

        private static void LoadLanguage()
        {
                string lang = Settings.Default.Language;
                AppConfig.Language = (lang == "FR" || lang == "EN") ? lang : "EN";
        }
}
