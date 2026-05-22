using System.Windows;
using System.Windows.Controls;

namespace HPPrinterScanner.Pages
{
    public partial class SettingsPage : Page
    {
        private readonly MainWindow? _main;

        public SettingsPage(MainWindow? main = null)
        {
            _main = main;
            InitializeComponent();
        }

        private void NetworkButton_Click(object sender, RoutedEventArgs e)
            => NavigationService?.Navigate(new NetworkSettingsPage(_main));

        private void ColumnsButton_Click(object sender, RoutedEventArgs e)
            => NavigationService?.Navigate(new ColumnsPage(_main));
    }
}
