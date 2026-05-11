using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using HPPrinterScanner.Models;
using HPPrinterScanner.Services;

namespace HPPrinterScanner.Pages
{
    public partial class InstalledPrintersPage : Page
    {
        private readonly PrinterEnumerationService _printerService    = new();
        private readonly PrintJobService           _jobService        = new();
        private readonly SpoolerService            _spoolerService    = new();
        private readonly PrinterManagementService  _managementService = new();
        private readonly PrinterStatusService      _statusService     = new();
        private readonly DispatcherTimer           _jobRefreshTimer   = new() { Interval = TimeSpan.FromSeconds(5) };

        private List<InstalledPrinter> _allPrinters = new();
        private bool _suppressFilterEvents;

        public InstalledPrintersPage()
        {
            InitializeComponent();
            _jobRefreshTimer.Tick += async (_, _) => await RefreshJobCountsAsync();

            Loaded   += async (_, _) => { ApplyColumnVisibility(); await LoadAsync(); };
            Unloaded += (_, _) => _jobRefreshTimer.Stop();
        }

        public void ApplyColumnVisibility()
        {
            var s = SettingsService.Instance.Current;

            foreach (var col in PrintersGrid.Columns)
            {
                col.Visibility = col.Header?.ToString() switch
                {
                    "Active Jobs" => s.ManageShowActiveJobs ? Visibility.Visible : Visibility.Collapsed,
                    "Port Type"   => s.ManageShowPortType   ? Visibility.Visible : Visibility.Collapsed,
                    "IP / Host"   => s.ManageShowIpHost     ? Visibility.Visible : Visibility.Collapsed,
                    "Port"        => s.ManageShowPort       ? Visibility.Visible : Visibility.Collapsed,
                    "Driver"      => s.ManageShowDriver     ? Visibility.Visible : Visibility.Collapsed,
                    _             => Visibility.Visible,
                };
            }
        }

        private async System.Threading.Tasks.Task LoadAsync()
        {
            LoadingBar.Visibility   = Visibility.Visible;
            RefreshButton.IsEnabled = false;
            EmptyState.Visibility   = Visibility.Collapsed;
            PrintersGrid.Visibility = Visibility.Collapsed;

            try
            {
                var printerTask = _printerService.GetInstalledPrintersAsync();
                var jobTask     = _jobService.GetAllJobsWithDiagnosticsAsync();
                await System.Threading.Tasks.Task.WhenAll(printerTask, jobTask);

                _allPrinters = printerTask.Result;
                ApplyJobCounts(jobTask.Result);
                ApplyDefaultFlag();
                ApplyFilter();
                _ = RefreshStatusAsync();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to read printer list:\n{ex.Message}",
                    "Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                _allPrinters = new();
                ApplyFilter();
            }
            finally
            {
                LoadingBar.Visibility   = Visibility.Collapsed;
                RefreshButton.IsEnabled = true;
                _jobRefreshTimer.Start();
            }
        }

        private async System.Threading.Tasks.Task RefreshJobCountsAsync()
        {
            if (_allPrinters.Count == 0) return;
            var result = await _jobService.GetAllJobsWithDiagnosticsAsync();
            ApplyJobCounts(result);
        }

        private async System.Threading.Tasks.Task RefreshStatusAsync()
        {
            var tasks = _allPrinters.Select(async p =>
            {
                var status = await _statusService.GetStatusAsync(p);
                p.Status = status;
            });
            await System.Threading.Tasks.Task.WhenAll(tasks);
        }

        private void ApplyDefaultFlag()
        {
            string defaultName = PrinterManagementService.GetDefaultPrinterName();
            foreach (var p in _allPrinters)
                p.IsDefault = string.Equals(p.Name, defaultName, StringComparison.OrdinalIgnoreCase);
        }

        private void ApplyJobCounts(JobScanResult result)
        {
            var countByPrinter = result.Jobs
                .GroupBy(j => j.PrinterName, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(g => g.Key, g => g.Count(), StringComparer.OrdinalIgnoreCase);

            foreach (var p in _allPrinters)
                p.ActiveJobCount = countByPrinter.TryGetValue(p.Name, out int n) ? n : 0;

            JobDiagLabel.Text = $"{DateTime.Now:HH:mm:ss} — {result.Diagnostics.Trim().Replace("\r\n", "  ·  ").Replace("\n", "  ·  ")}";
        }

        private void ApplyFilter()
        {
            bool tcpIp = FilterCheckTcpIp.IsChecked ?? true;
            bool ipp   = FilterCheckIpp.IsChecked   ?? true;
            bool wsd   = FilterCheckWsd.IsChecked   ?? true;
            bool local = FilterCheckLocal.IsChecked ?? true;
            bool all   = tcpIp && ipp && wsd && local;

            IEnumerable<InstalledPrinter> view = all
                ? _allPrinters
                : _allPrinters.Where(p =>
                    (tcpIp && p.PortType == PrinterPortType.TcpIp)  ||
                    (ipp   && p.PortType == PrinterPortType.Ipp)     ||
                    (wsd   && p.PortType == PrinterPortType.Wsd)     ||
                    (local && p.PortType == PrinterPortType.Local));

            var list = view.ToList();
            PrintersGrid.ItemsSource = list;

            bool empty = list.Count == 0;
            EmptyState.Visibility   = empty ? Visibility.Visible  : Visibility.Collapsed;
            PrintersGrid.Visibility = empty ? Visibility.Collapsed : Visibility.Visible;
        }

        private void PortTypeFilterButton_Click(object sender, RoutedEventArgs e)
        {
            PortTypeFilterPopup.PlacementTarget = (UIElement)sender;
            PortTypeFilterPopup.IsOpen = true;
        }

        private void FilterCheckAll_CheckedChanged(object sender, RoutedEventArgs e)
        {
            // Guard: individual checkboxes may not exist yet during XAML init
            if (_suppressFilterEvents || FilterCheckTcpIp == null) return;
            _suppressFilterEvents = true;
            bool check = FilterCheckAll.IsChecked == true;
            FilterCheckTcpIp.IsChecked = check;
            FilterCheckIpp.IsChecked   = check;
            FilterCheckWsd.IsChecked   = check;
            FilterCheckLocal.IsChecked = check;
            _suppressFilterEvents = false;
            ApplyFilter();
        }

        private void FilterCheck_Changed(object sender, RoutedEventArgs e)
        {
            // Guard: wait until the last checkbox in XAML order is initialized
            if (_suppressFilterEvents || FilterCheckLocal == null) return;
            _suppressFilterEvents = true;
            FilterCheckAll.IsChecked =
                FilterCheckTcpIp.IsChecked == true &&
                FilterCheckIpp.IsChecked   == true &&
                FilterCheckWsd.IsChecked   == true &&
                FilterCheckLocal.IsChecked == true;
            _suppressFilterEvents = false;
            ApplyFilter();
        }

        private async void RefreshButton_Click(object sender, RoutedEventArgs e)
            => await LoadAsync();

        private void PrintersGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            RemoveButton.IsEnabled = PrintersGrid.SelectedItem is InstalledPrinter;
        }

        private void PrintersRow_PreviewMouseRightButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (sender is DataGridRow row)
                row.IsSelected = true;
        }

        private async void RestartSpoolerButton_Click(object sender, RoutedEventArgs e)
        {
            RestartSpoolerButton.IsEnabled = false;
            try
            {
                await _spoolerService.RestartAsync();
                await LoadAsync();
            }
            catch (OperationCanceledException)
            {
                // User dismissed the UAC prompt — do nothing.
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to restart the print spooler:\n{ex.Message}",
                    "Restart Spooler", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
            finally
            {
                RestartSpoolerButton.IsEnabled = true;
            }
        }

        private async void ClearJobsButton_Click(object sender, RoutedEventArgs e)
        {
            if (PrintersGrid.SelectedItem is not InstalledPrinter printer) return;

            var result = MessageBox.Show(
                $"Clear all print jobs for \"{printer.Name}\"?",
                "Clear Print Jobs", MessageBoxButton.OKCancel, MessageBoxImage.Question);
            if (result != MessageBoxResult.OK) return;

            try
            {
                await _jobService.PurgeQueueAsync(printer.Name);
                await LoadAsync();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to clear print jobs:\n{ex.Message}",
                    "Clear Print Jobs", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private async void SetDefaultButton_Click(object sender, RoutedEventArgs e)
        {
            if (PrintersGrid.SelectedItem is not InstalledPrinter printer) return;

            try
            {
                await _managementService.SetDefaultAsync(printer.Name);

                foreach (var p in _allPrinters)
                    p.IsDefault = string.Equals(p.Name, printer.Name, StringComparison.OrdinalIgnoreCase);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to set default printer:\n{ex.Message}",
                    "Set Default Printer", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void PropertiesButton_Click(object sender, RoutedEventArgs e)
        {
            if (PrintersGrid.SelectedItem is not InstalledPrinter printer) return;
            try
            {
                _managementService.ShowProperties(printer.Name);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to open printer properties:\n{ex.Message}",
                    "Printer Properties", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void PreferencesButton_Click(object sender, RoutedEventArgs e)
        {
            if (PrintersGrid.SelectedItem is not InstalledPrinter printer) return;
            try
            {
                _managementService.ShowPreferences(printer.Name);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to open printer preferences:\n{ex.Message}",
                    "Printer Preferences", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private async void RemoveButton_Click(object sender, RoutedEventArgs e)
        {
            if (PrintersGrid.SelectedItem is not InstalledPrinter printer) return;

            var result = MessageBox.Show(
                $"Remove \"{printer.Name}\"?\n\nThis will permanently remove the printer from this machine.",
                "Remove Printer", MessageBoxButton.OKCancel, MessageBoxImage.Warning);
            if (result != MessageBoxResult.OK) return;

            RemoveButton.IsEnabled = false;
            try
            {
                await _managementService.RemoveAsync(printer.Name);
                _allPrinters.Remove(printer);
                ApplyFilter();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to remove \"{printer.Name}\":\n{ex.Message}\n\nTry running the application as administrator.",
                    "Remove Printer", MessageBoxButton.OK, MessageBoxImage.Warning);
                RemoveButton.IsEnabled = true;
            }
        }
    }
}
