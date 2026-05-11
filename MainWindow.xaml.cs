using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using HPPrinterScanner.Pages;
using HPPrinterScanner.Services;
using Wpf.Ui.Controls;

namespace HPPrinterScanner
{
    public partial class MainWindow : FluentWindow
    {
        private ScanPage?              _scanPage;
        private InstalledPrintersPage? _managePage;

        public MainWindow()
        {
            InitializeComponent();
            NavigateToScan();
        }

        private void Navigate(Func<Page> factory, string title)
        {
            try
            {
                PageFrame.Navigate(factory());
                StatusBar.Title   = title;
                StatusBar.Message = string.Empty;
            }
            catch (Exception ex)
            {
                StatusBar.Title   = "Error";
                StatusBar.Message = $"Could not open '{title}': {ex.Message}";
            }
        }

        private void NavigateToScan()
        {
            _scanPage = new ScanPage();
            PageFrame.Navigate(_scanPage);
            StatusBar.Title   = "Scan Network";
            var loadErr = SettingsService.Instance.LastLoadError;
            if (loadErr != null && !loadErr.StartsWith("First run"))
                StatusBar.Message = $"Settings: {loadErr}";
            else
                StatusBar.Message = $"Settings loaded from: {SettingsService.FilePath}";
        }

        // Called by SettingsPage when column visibility changes.
        public void RefreshScanColumns()   => _scanPage?.ApplyColumnVisibility();
        public void RefreshManageColumns() => _managePage?.ApplyColumnVisibility();

        private void NavScan_Click(object sender, MouseButtonEventArgs e)
        {
            if (_scanPage == null) NavigateToScan();
            else PageFrame.Navigate(_scanPage);
            StatusBar.Title   = "Scan Network";
            StatusBar.Message = string.Empty;
        }

        private void NavInstalled_Click(object sender, MouseButtonEventArgs e)
        {
            _managePage ??= new InstalledPrintersPage();
            PageFrame.Navigate(_managePage);
            StatusBar.Title   = "Manage Printers";
            StatusBar.Message = string.Empty;
        }

        private void NavSettings_Click(object sender, MouseButtonEventArgs e)
            => Navigate(() => new SettingsPage(this), "Settings");
    }
}
