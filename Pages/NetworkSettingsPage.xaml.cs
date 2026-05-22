using System.Windows;
using System.Windows.Controls;
using HPPrinterScanner.Services;

namespace HPPrinterScanner.Pages
{
    public partial class NetworkSettingsPage : Page
    {
        private readonly MainWindow? _main;
        private bool _initialising;

        public NetworkSettingsPage(MainWindow? main = null)
        {
            _main = main;
            InitializeComponent();

            _initialising = true;
            AutoSubnetLabel.Text = NetworkScannerService.DetectLocalSubnet() + ".x";

            bool hasOverride = !string.IsNullOrWhiteSpace(SettingsService.Instance.Current.SubnetOverride);
            OverrideToggle.IsChecked     = hasOverride;
            SubnetOverrideBox.Text       = SettingsService.Instance.Current.SubnetOverride;
            SubnetOverrideBox.Visibility = hasOverride ? Visibility.Visible : Visibility.Collapsed;
            _initialising = false;
        }

        private void Back_Click(object sender, RoutedEventArgs e)
            => NavigationService?.GoBack();

        private void OverrideToggle_Click(object sender, RoutedEventArgs e)
        {
            if (_initialising) return;
            bool on = OverrideToggle.IsChecked == true;
            SubnetOverrideBox.Visibility = on ? Visibility.Visible : Visibility.Collapsed;

            if (!on)
            {
                SettingsService.Instance.Current.SubnetOverride = string.Empty;
                SettingsService.Instance.Save();
            }
        }

        private void SubnetBox_LostFocus(object sender, RoutedEventArgs e)
        {
            if (OverrideToggle.IsChecked != true) return;
            SettingsService.Instance.Current.SubnetOverride = SubnetOverrideBox.Text.Trim();
            SettingsService.Instance.Save();
        }
    }
}
