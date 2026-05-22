using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace HPPrinterScanner.Services
{
    public class PrinterInstallService
    {
        private const string ScriptName = "Install-Printer.ps1";

        public static string LocateScript()
        {
            // 1. Next to the executable (deployed builds or scripts copied to output).
            string exeDir = AppContext.BaseDirectory;
            string candidate = Path.Combine(exeDir, ScriptName);
            if (File.Exists(candidate)) return candidate;

            // 2. Walk up the directory tree (dev builds: script lives in repo root,
            //    exe is several levels deep in bin/Debug/...).
            string? dir = exeDir;
            for (int i = 0; i < 6; i++)
            {
                dir = Path.GetDirectoryName(dir);
                if (dir is null) break;
                candidate = Path.Combine(dir, ScriptName);
                if (File.Exists(candidate)) return candidate;
            }

            throw new FileNotFoundException(
                $"Could not find {ScriptName}. " +
                "Place it next to the application executable or in the repository root.");
        }

        public async Task InstallAsync(
            string ip,
            string driverKey,
            string printerName,
            bool setDefault,
            IProgress<string>? progress = null,
            CancellationToken ct = default)
        {
            string scriptPath = LocateScript();
            progress?.Report($"Launching installer for driver {driverKey}…");

            string args =
                $"-ExecutionPolicy Bypass -NoProfile -File \"{scriptPath}\"" +
                $" -printerIP \"{ip}\"" +
                $" -model \"{driverKey}\"" +
                (string.IsNullOrWhiteSpace(printerName) ? "" : $" -printerName \"{printerName}\"") +
                (setDefault ? " -setDefault" : "");

            // Printer driver installation requires elevation.
            var psi = new ProcessStartInfo("powershell.exe", args)
            {
                Verb            = "runas",
                UseShellExecute = true,
            };

            Process proc;
            try
            {
                proc = Process.Start(psi)
                    ?? throw new InvalidOperationException("Failed to start PowerShell.");
            }
            catch (System.ComponentModel.Win32Exception ex) when (ex.NativeErrorCode == 1223)
            {
                // User cancelled the UAC prompt.
                throw new OperationCanceledException("Installation cancelled (UAC prompt dismissed).", ex);
            }

            await proc.WaitForExitAsync(ct);

            if (proc.ExitCode != 0)
                throw new InvalidOperationException(
                    $"Install-Printer.ps1 exited with code {proc.ExitCode}. " +
                    "Check the PowerShell window for details.");
        }
    }
}
