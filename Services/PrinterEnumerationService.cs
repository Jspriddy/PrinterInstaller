using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Win32;
using HPPrinterScanner.Models;

namespace HPPrinterScanner.Services
{
    // Uses the registry instead of WMI. WMI (System.Management) can throw
    // unmanageable COM exceptions (AccessViolationException) that crash the
    // process on .NET Core even inside a catch block.
    public class PrinterEnumerationService
    {
        private const string PrintersRoot =
            @"SYSTEM\CurrentControlSet\Control\Print\Printers";

        private const string TcpPortsRoot =
            @"SYSTEM\CurrentControlSet\Control\Print\Monitors\Standard TCP/IP Port\Ports";

        public Task<List<InstalledPrinter>> GetInstalledPrintersAsync()
            => Task.Run(GetInstalledPrinters);

        private static List<InstalledPrinter> GetInstalledPrinters()
        {
            var tcpIpPorts = BuildTcpIpPortMap();
            var results    = new List<InstalledPrinter>();

            using var printersKey = Registry.LocalMachine.OpenSubKey(PrintersRoot);
            if (printersKey == null) return results;

            foreach (string printerName in printersKey.GetSubKeyNames())
            {
                using var pk = printersKey.OpenSubKey(printerName);
                if (pk == null) continue;

                string portName   = pk.GetValue("Port")?.ToString()           ?? string.Empty;
                string driverName = pk.GetValue("Printer Driver")?.ToString() ?? string.Empty;

                var (portType, ipAddress) = ClassifyPort(portName, tcpIpPorts);

                results.Add(new InstalledPrinter
                {
                    Name       = printerName,
                    PortName   = portName,
                    DriverName = driverName,
                    IpAddress  = ipAddress,
                    PortType   = portType,
                });
            }

            results.Sort((a, b) => string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase));
            return results;
        }

        private static Dictionary<string, string> BuildTcpIpPortMap()
        {
            var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            try
            {
                using var portsKey = Registry.LocalMachine.OpenSubKey(TcpPortsRoot);
                if (portsKey == null) return map;

                foreach (string portName in portsKey.GetSubKeyNames())
                {
                    using var pk = portsKey.OpenSubKey(portName);
                    if (pk == null) continue;

                    // Standard TCP/IP Port stores the target as HostName; some drivers use IPAddress
                    string host = pk.GetValue("HostName")?.ToString()
                               ?? pk.GetValue("IPAddress")?.ToString()
                               ?? string.Empty;

                    map[portName] = host;
                }
            }
            catch { /* registry key absent — no IP resolution */ }

            return map;
        }

        private static (PrinterPortType type, string ip) ClassifyPort(
            string portName, Dictionary<string, string> tcpIpPorts)
        {
            if (string.IsNullOrEmpty(portName))
                return (PrinterPortType.Unknown, string.Empty);

            if (tcpIpPorts.TryGetValue(portName, out string? ip))
                return (PrinterPortType.TcpIp, ip ?? string.Empty);

            string upper = portName.ToUpperInvariant();

            if (upper.StartsWith("WSD", StringComparison.Ordinal))
                return (PrinterPortType.Wsd, string.Empty);

            if (upper.StartsWith("HTTP://",  StringComparison.Ordinal) ||
                upper.StartsWith("HTTPS://", StringComparison.Ordinal) ||
                upper.StartsWith("IPP://",   StringComparison.Ordinal) ||
                upper.StartsWith("IPPS://",  StringComparison.Ordinal))
                return (PrinterPortType.Ipp, ExtractHost(portName));

            if (upper.StartsWith("USB", StringComparison.Ordinal) ||
                upper.StartsWith("LPT", StringComparison.Ordinal) ||
                upper.StartsWith("COM", StringComparison.Ordinal) ||
                upper is "FILE:" or "PORTPROMPT:" or "NUL:")
                return (PrinterPortType.Local, string.Empty);

            return (PrinterPortType.Unknown, string.Empty);
        }

        private static string ExtractHost(string url)
        {
            try   { return new Uri(url).Host; }
            catch { return string.Empty; }
        }
    }
}
