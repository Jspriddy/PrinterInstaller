using System;
using System.Windows;
using System.Windows.Controls;
using HPPrinterScanner.Services;

namespace HPPrinterScanner.Pages
{
    public partial class SettingsPage : Page
    {
        private bool _initialising;
        private readonly MainWindow? _main;

        public SettingsPage(MainWindow? main = null)
        {
            _main = main;
            InitializeComponent();
            _initialising = true;

            AutoSubnetLabel.Text = NetworkScannerService.DetectLocalSubnet() + ".x";

            bool hasOverride = !string.IsNullOrWhiteSpace(SettingsService.Instance.Current.SubnetOverride);
            OverrideToggle.IsChecked = hasOverride;
            SubnetOverrideBox.Text       = SettingsService.Instance.Current.SubnetOverride;
            SubnetOverrideBox.Visibility = hasOverride ? Visibility.Visible : Visibility.Collapsed;

            var s = SettingsService.Instance.Current;
            ColMacToggle.IsChecked      = s.ShowMacAddress;
            ColHostnameToggle.IsChecked = s.ShowHostname;
            ColModelToggle.IsChecked    = s.ShowModel;
            ColPortsToggle.IsChecked    = s.ShowPorts;

            ManageColActiveJobsToggle.IsChecked = s.ManageShowActiveJobs;
            ManageColPortTypeToggle.IsChecked   = s.ManageShowPortType;
            ManageColIpHostToggle.IsChecked     = s.ManageShowIpHost;
            ManageColPortToggle.IsChecked       = s.ManageShowPort;
            ManageColDriverToggle.IsChecked     = s.ManageShowDriver;

            _initialising = false;
        }

        private void OverrideToggle_Toggled(object sender, RoutedEventArgs e)
        {
            if (_initialising) return;
            SubnetOverrideBox.Visibility = OverrideToggle.IsChecked == true
                ? Visibility.Visible : Visibility.Collapsed;
        }

        private void SubnetOverrideBox_TextChanged(object sender, TextChangedEventArgs e) { }

        private void ColToggle_Changed(object sender, RoutedEventArgs e) { }
        private void ManageColToggle_Changed(object sender, RoutedEventArgs e) { }

        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var s = SettingsService.Instance.Current;

                s.SubnetOverride = OverrideToggle.IsChecked == true
                    ? SubnetOverrideBox.Text.Trim()
                    : string.Empty;

                s.ShowMacAddress = ColMacToggle.IsChecked      == true;
                s.ShowHostname   = ColHostnameToggle.IsChecked == true;
                s.ShowModel      = ColModelToggle.IsChecked    == true;
                s.ShowPorts      = ColPortsToggle.IsChecked    == true;

                s.ManageShowActiveJobs = ManageColActiveJobsToggle.IsChecked == true;
                s.ManageShowPortType   = ManageColPortTypeToggle.IsChecked   == true;
                s.ManageShowIpHost     = ManageColIpHostToggle.IsChecked     == true;
                s.ManageShowPort       = ManageColPortToggle.IsChecked       == true;
                s.ManageShowDriver     = ManageColDriverToggle.IsChecked     == true;

                SettingsService.Instance.Save();
                _main?.RefreshScanColumns();
                _main?.RefreshManageColumns();
                SaveStatus.Text = $"Saved → {SettingsService.FilePath}";
            }
            catch (Exception ex)
            {
                SaveStatus.Text = $"Error: {ex.Message}";
            }
        }
    }
}
