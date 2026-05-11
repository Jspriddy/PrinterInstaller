using System;
using System.Net.NetworkInformation;
using System.Printing;
using System.Threading.Tasks;
using HPPrinterScanner.Models;

namespace HPPrinterScanner.Services
{
    public class PrinterStatusService
    {
        public async Task<PrinterStatus> GetStatusAsync(InstalledPrinter printer)
        {
            PrinterStatus sys = GetSystemStatus(printer.Name);

            // Error flags always take priority.
            if (sys == PrinterStatus.Error)
                return PrinterStatus.Error;

            // Active jobs reported by the spooler → Printing.
            if (printer.ActiveJobCount > 0)
                return PrinterStatus.Printing;

            // Power-save mode detected by System.Printing.
            if (sys == PrinterStatus.Sleeping)
                return PrinterStatus.Sleeping;

            // Spooler says offline (no physical connection).
            if (sys == PrinterStatus.Offline)
                return PrinterStatus.Offline;

            // For network printers confirm reachability with a ping.
            if (!string.IsNullOrEmpty(printer.IpAddress))
            {
                bool up = await PingAsync(printer.IpAddress);
                return up ? PrinterStatus.Online : PrinterStatus.Offline;
            }

            // Local printer with no reported errors → online.
            return PrinterStatus.Online;
        }

        private static PrinterStatus GetSystemStatus(string printerName)
        {
            try
            {
                using var server = new LocalPrintServer();
                using var queue  = server.GetPrintQueue(printerName);
                var s = queue.QueueStatus;

                if (s.HasFlag(PrintQueueStatus.Error)            ||
                    s.HasFlag(PrintQueueStatus.PaperJam)         ||
                    s.HasFlag(PrintQueueStatus.PaperOut)         ||
                    s.HasFlag(PrintQueueStatus.NoToner)          ||
                    s.HasFlag(PrintQueueStatus.TonerLow)         ||
                    s.HasFlag(PrintQueueStatus.UserIntervention))
                    return PrinterStatus.Error;

                if (s.HasFlag(PrintQueueStatus.Offline))
                    return PrinterStatus.Offline;

                if (s.HasFlag(PrintQueueStatus.PowerSave))
                    return PrinterStatus.Sleeping;
            }
            catch { /* printer inaccessible — fall through to ping */ }

            return PrinterStatus.Unknown;
        }

        private static async Task<bool> PingAsync(string host)
        {
            try
            {
                using var ping = new Ping();
                var reply = await ping.SendPingAsync(host, 2000);
                return reply.Status == IPStatus.Success;
            }
            catch { return false; }
        }
    }
}
