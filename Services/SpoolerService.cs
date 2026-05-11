using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Threading.Tasks;

namespace HPPrinterScanner.Services
{
    public class SpoolerService
    {
        public Task RestartAsync() => Task.Run(Restart);

        private static void Restart()
        {
            var psi = new ProcessStartInfo
            {
                FileName        = "powershell.exe",
                Arguments       = "-NoProfile -NonInteractive -WindowStyle Hidden " +
                                  "-Command \"Restart-Service -Name Spooler -Force\"",
                Verb            = "runas",
                UseShellExecute = true,
                WindowStyle     = ProcessWindowStyle.Hidden,
            };

            Process process;
            try
            {
                process = Process.Start(psi)
                    ?? throw new InvalidOperationException("Failed to launch elevated process.");
            }
            catch (Win32Exception ex) when (ex.NativeErrorCode == 1223)
            {
                // User dismissed the UAC prompt — treat as cancellation, not an error.
                throw new OperationCanceledException("UAC prompt was dismissed.", ex);
            }

            if (!process.WaitForExit(30_000))
                throw new TimeoutException("Spooler restart timed out after 30 seconds.");

            if (process.ExitCode != 0)
                throw new InvalidOperationException(
                    $"Spooler restart failed (PowerShell exit code {process.ExitCode}).");
        }
    }
}
