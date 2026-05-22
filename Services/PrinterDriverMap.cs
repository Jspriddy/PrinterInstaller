using System;

namespace HPPrinterScanner.Services
{
    public static class PrinterDriverMap
    {
        public record DriverInfo(string Key, string DisplayName);

        // Ordered: first match wins. Patterns are substrings of the SNMP model string.
        private static readonly (string Pattern, string Key, string DisplayName)[] Entries =
        {
            ("E52645",  "HPLJE52645",      "HP LaserJet MFP E52645 PCL-6 (V4)"),
            ("E47528",  "HPLJE47528",      "HP Color LaserJet MFP E47528 PCL-6 (V4)"),
            ("E42540",  "HPLJE42540",      "HP LaserJet MFP E42540 PCL-6 (V4)"),
            ("M437",    "HPLJM437-M440",   "HP LaserJet MFP M437-M443 PCL6"),
            ("M438",    "HPLJM437-M440",   "HP LaserJet MFP M437-M443 PCL6"),
            ("M439",    "HPLJM437-M440",   "HP LaserJet MFP M437-M443 PCL6"),
            ("M440",    "HPLJM437-M440",   "HP LaserJet MFP M437-M443 PCL6"),
            ("M443",    "HPLJM437-M440",   "HP LaserJet MFP M437-M443 PCL6"),
            ("E45028",  "HPCLJE45028",     "HP Color LaserJet E45028 PCL-6 (V4)"),
            ("E40040",  "HPLJE40040",      "HP LaserJet E40040 PCL-6 (V4)"),
            ("M426",    "HPLJM426f-M427f", "HP LaserJet Pro MFP M426f-M427f PCL-6"),
            ("M427",    "HPLJM426f-M427f", "HP LaserJet Pro MFP M426f-M427f PCL-6"),
            ("M404",    "HPLJM404-M405",   "HP LaserJet Pro M404-M405 PCL-6 (V4)"),
            ("M405",    "HPLJM404-M405",   "HP LaserJet Pro M404-M405 PCL-6 (V4)"),
            ("M453",    "HPCLJPM453-4",    "HP Color LaserJet Pro M453-4 PCL-6 (V4)"),
            ("M454",    "HPCLJPM453-4",    "HP Color LaserJet Pro M453-4 PCL-6 (V4)"),
        };

        public static DriverInfo Resolve(string snmpModel)
        {
            if (string.IsNullOrWhiteSpace(snmpModel))
                throw new InvalidOperationException(
                    "Model has not been detected yet. Wait for the scan to finish and try again.");

            foreach (var (pattern, key, displayName) in Entries)
            {
                if (snmpModel.Contains(pattern, StringComparison.OrdinalIgnoreCase))
                    return new DriverInfo(key, displayName);
            }

            throw new InvalidOperationException(
                $"No supported driver found for \"{snmpModel}\".\n\n" +
                "Supported models: E52645, E47528, E42540, M437–M443, E45028, " +
                "E40040, M426/M427, M404/M405, M453/M454.\n\n" +
                "Add a new entry to PrinterDriverData.ps1 and PrinterDriverMap.cs to support this model.");
        }
    }
}
