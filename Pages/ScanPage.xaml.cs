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

        private async void PromptInstall(NetworkPrinter printer)
        {
            // Resolve the driver before showing any UI — fail fast with a clear message.
            PrinterDriverMap.DriverInfo driverInfo;
            try
            {
                driverInfo = PrinterDriverMap.Resolve(printer.Model);
            }
            catch (InvalidOperationException ex)
            {
                MessageBox.Show(ex.Message, "Driver Not Found",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            string printerLabel = string.IsNullOrEmpty(printer.Hostname)
                ? printer.IpAddress
                : $"{printer.Hostname} ({printer.IpAddress})";

            var dialog = new InstallDialog(printerLabel, printer.Model, driverInfo.DisplayName, driverInfo.DisplayName)
            {
                Owner = Window.GetWindow(this)
            };

            if (dialog.ShowDialog() != true) return;

            printer.Status      = "Installing…";
            InstallButton.IsEnabled = false;

            try
            {
                var svc      = new PrinterInstallService();
                var progress = new Progress<string>(msg => printer.Status = msg);
                await svc.InstallAsync(
                    printer.IpAddress,
                    driverInfo.Key,
                    dialog.ChosenName,
                    dialog.SetAsDefault,
                    progress);

                printer.Status = "Installed";
            }
            catch (OperationCanceledException)
            {
                printer.Status = "Cancelled";
            }
            catch (Exception ex)
            {
                printer.Status = "Install failed";
                MessageBox.Show(ex.Message, "Install Failed",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                InstallButton.IsEnabled = ResultsGrid.SelectedItem is NetworkPrinter;
            }
        }
    }
}
