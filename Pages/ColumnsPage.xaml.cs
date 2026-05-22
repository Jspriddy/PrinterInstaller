using System.Windows;
using System.Windows.Controls;
using HPPrinterScanner.Services;

namespace HPPrinterScanner.Pages
{
    public partial class ColumnsPage : Page
    {
        private readonly MainWindow? _main;
        private bool _initialising;

        public ColumnsPage(MainWindow? main = null)
        {
            _main = main;
            InitializeComponent();

            _initialising = true;
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

        private void Back_Click(object sender, RoutedEventArgs e)
            => NavigationService?.GoBack();

        private void ColToggle_Changed(object sender, RoutedEventArgs e)
        {
            if (_initialising) return;
            var s = SettingsService.Instance.Current;
            s.ShowMacAddress = ColMacToggle.IsChecked      == true;
            s.ShowHostname   = ColHostnameToggle.IsChecked == true;
            s.ShowModel      = ColModelToggle.IsChecked    == true;
            s.ShowPorts      = ColPortsToggle.IsChecked    == true;
            SettingsService.Instance.Save();
            _main?.RefreshScanColumns();
        }

        private void ManageColToggle_Changed(object sender, RoutedEventArgs e)
        {
            if (_initialising) return;
            var s = SettingsService.Instance.Current;
            s.ManageShowActiveJobs = ManageColActiveJobsToggle.IsChecked == true;
            s.ManageShowPortType   = ManageColPortTypeToggle.IsChecked   == true;
            s.ManageShowIpHost     = ManageColIpHostToggle.IsChecked     == true;
            s.ManageShowPort       = ManageColPortToggle.IsChecked       == true;
            s.ManageShowDriver     = ManageColDriverToggle.IsChecked     == true;
            SettingsService.Instance.Save();
            _main?.RefreshManageColumns();
        }
    }
}
