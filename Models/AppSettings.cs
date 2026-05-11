namespace HPPrinterScanner.Models
{
    public class AppSettings
    {
        public string SubnetOverride { get; set; } = string.Empty;

        // Install a Printer — scan results columns
        public bool ShowMacAddress { get; set; } = true;
        public bool ShowHostname   { get; set; } = true;
        public bool ShowModel      { get; set; } = true;
        public bool ShowPorts      { get; set; } = true;

        // Manage Printers — columns
        public bool ManageShowActiveJobs { get; set; } = true;
        public bool ManageShowPortType   { get; set; } = true;
        public bool ManageShowIpHost     { get; set; } = true;
        public bool ManageShowPort       { get; set; } = true;
        public bool ManageShowDriver     { get; set; } = true;
    }
}
