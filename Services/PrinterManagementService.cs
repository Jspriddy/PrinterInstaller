using System;
using System.Diagnostics;
using System.Printing;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Microsoft.Win32;

namespace HPPrinterScanner.Services
{
    public class PrinterManagementService
    {
        public Task RemoveAsync(string printerName)
            => Task.Run(() => Remove(printerName));

        public void ShowProperties(string printerName)
            => LaunchPrintUI($"/p /n \"{printerName}\"");

        public void ShowPreferences(string printerName)
            => LaunchPrintUI($"/e /n \"{printerName}\"");

        public Task SetDefaultAsync(string printerName)
            => Task.Run(() =>
            {
                if (!SetDefaultPrinter(printerName))
                    throw new InvalidOperationException(
                        $"SetDefaultPrinter failed (Win32 error {Marshal.GetLastWin32Error()})");
            });

        public static string GetDefaultPrinterName()
        {
            try
            {
                const string key = @"Software\Microsoft\Windows NT\CurrentVersion\Windows";
                using var regKey = Registry.CurrentUser.OpenSubKey(key);
                string? device = regKey?.GetValue("Device")?.ToString();
                if (string.IsNullOrEmpty(device)) return string.Empty;
                int comma = device.IndexOf(',');
                return comma > 0 ? device[..comma] : device;
            }
            catch { return string.Empty; }
        }

        [DllImport("winspool.drv", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern bool SetDefaultPrinter(string Name);

        private static void Remove(string printerName)
        {
            PrintServer.DeletePrintQueue(printerName);
        }

        private static void LaunchPrintUI(string args)
        {
            Process.Start(new ProcessStartInfo
            {
                FileName        = "rundll32.exe",
                Arguments       = $"printui.dll,PrintUIEntry {args}",
                UseShellExecute = false,
            });
        }
    }
}
