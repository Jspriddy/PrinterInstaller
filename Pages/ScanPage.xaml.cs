using System;
using System.Collections.ObjectModel;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using HPPrinterScanner.Models;
using HPPrinterScanner.Services;

namespace HPPrinterScanner.Pages
{
    public partial class ScanPage : Page
    {
        private readonly NetworkScannerService _scanner = new();
        private CancellationTokenSource? _cts;

        public ObservableCollection<NetworkPrinter> Results { get; } = new();

        public ScanPage()
        {
            InitializeComponent();
            ResultsGrid.ItemsSource = Results;
            UpdateSubnetLabel();
            Loaded += (_, _) => ApplyColumnVisibility();
        }

        public void ApplyColumnVisibility()
        {
            var s = SettingsService.Instance.Current;
            foreach (var col in ResultsGrid.Columns)
            {
                col.Visibility = col.Header?.ToString() switch
                {
                    "MAC Address" => s.ShowMacAddress ? Visibility.Visible : Visibility.Collapsed,
                    "Hostname"    => s.ShowHostname   ? Visibility.Visible : Visibility.Collapsed,
                    "Model"       => s.ShowModel      ? Visibility.Visible : Visibility.Collapsed,
                    "Ports"       => s.ShowPorts      ? Visibility.Visible : Visibility.Collapsed,
                    _             => Visibility.Visible,
                };
            }
        }

        private void UpdateSubnetLabel()
        {
            string subnet = SettingsService.Instance.EffectiveSubnet();
            SubnetLabel.Text = $"Scanning {subnet}.1–254";
        }

        private async void ScanButton_Click(object sender, RoutedEventArgs e)
        {
            string subnet = SettingsService.Instance.EffectiveSubnet();

            Results.Clear();
            ScanProgress.Value = 0;
            ProgressLabel.Text = "0 / 254";
            SubnetLabel.Text = $"Scanning {subnet}.1–254";
            ScanButton.IsEnabled = false;
            CancelButton.IsEnabled = true;

            _cts = new CancellationTokenSource();

            var progress = new Progress<(int done, int total, NetworkPrinter? found)>(report =>
            {
                ScanProgress.Value = report.done;
                // Phase 1: done < total → ping sweep in progress
                // Phase 2: done == total → probing HP-MAC devices, printers appear as found
                ProgressLabel.Text = report.done < report.total
                    ? $"Pinging {report.done} / {report.total}"
                    : $"Found {Results.Count} HP device(s)";
                if (report.found is not null)
                    Results.Add(report.found);
            });

            try
            {
                await _scanner.ScanAsync(subnet, progress, _cts.Token);
            }
            catch (OperationCanceledException) { }
            finally
            {
                ScanButton.IsEnabled = true;
                CancelButton.IsEnabled = false;
                ProgressLabel.Text = $"Done — {Results.Count} HP printer(s) found";
            }
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
            => _cts?.Cancel();

        private void ResultsGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            InstallButton.IsEnabled = ResultsGrid.SelectedItem is NetworkPrinter;
        }

        private void ResultsGrid_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (ResultsGrid.SelectedItem is NetworkPrinter printer)
                PromptInstall(printer);
        }

        private void InstallButton_Click(object sender, RoutedEventArgs e)
        {
            if (ResultsGrid.SelectedItem is NetworkPrinter printer)
                PromptInstall(printer);
        }

        private void PromptInstall(NetworkPrinter printer)
        {
            // TODO: replace with real install flow
            MessageBox.Show($"Install \"{printer.Hostname ?? printer.IpAddress}\"?\n\nThis will be wired up in a future step.",
                "Install Printer", MessageBoxButton.OKCancel, MessageBoxImage.Information);
        }
    }
}
