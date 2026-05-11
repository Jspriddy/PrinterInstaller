using System;
using System.IO;
using System.Windows;
using System.Windows.Threading;
using Wpf.Ui.Appearance;

namespace HPPrinterScanner
{
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            DispatcherUnhandledException             += OnDispatcherException;
            AppDomain.CurrentDomain.UnhandledException += OnDomainException;

            ApplicationThemeManager.Apply(ApplicationTheme.Dark);

            new MainWindow().Show();
        }

        private static void OnDispatcherException(object sender, DispatcherUnhandledExceptionEventArgs e)
        {
            LogAndShow("UI thread exception", e.Exception);
            e.Handled = true;   // prevent process termination
        }

        private static void OnDomainException(object sender, UnhandledExceptionEventArgs e)
        {
            if (e.ExceptionObject is Exception ex)
                LogAndShow("Background thread exception", ex);
        }

        private static void LogAndShow(string context, Exception ex)
        {
            string logPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "HPPrinterScanner", "error.log");

            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(logPath)!);
                File.AppendAllText(logPath,
                    $"[{DateTime.Now:u}] {context}: {ex}\r\n\r\n");
            }
            catch { /* log write failed — don't crash again */ }

            MessageBox.Show(
                $"{context}:\n\n{ex.GetType().Name}: {ex.Message}\n\nDetails written to:\n{logPath}",
                "Unhandled Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }
}
