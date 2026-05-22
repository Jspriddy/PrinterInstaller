using System.Windows;

namespace HPPrinterScanner.Pages
{
    public partial class InstallDialog : Window
    {
        public string ChosenName   => PrinterNameBox.Text.Trim();
        public bool   SetAsDefault => SetDefaultCheck.IsChecked == true;

        public InstallDialog(string printerLabel, string model, string driverDisplayName, string defaultName)
        {
            InitializeComponent();
            PrinterLabel.Text   = printerLabel;
            ModelLabel.Text     = model;
            DriverLabel.Text    = driverDisplayName;
            PrinterNameBox.Text = defaultName;
        }

        private void InstallBtn_Click(object sender, RoutedEventArgs e)
            => DialogResult = true;

        private void CancelBtn_Click(object sender, RoutedEventArgs e)
            => DialogResult = false;
    }
}
