using System;
using System.Collections.Generic;
using System.Printing;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using HPPrinterScanner.Models;

namespace HPPrinterScanner.Services
{
    public record JobScanResult(List<PrintJob> Jobs, string Diagnostics);

    public class PrintJobService
    {
        public Task<List<PrintJob>> GetAllJobsAsync()
            => Task.Run(GetAllJobs);

        public Task<JobScanResult> GetAllJobsWithDiagnosticsAsync()
            => Task.Run(GetAllJobsWithDiagnostics);

        public Task PurgeQueueAsync(string printerName)
            => Task.Run(() => PurgeQueue(printerName));

        private static List<PrintJob> GetAllJobs()
            => GetAllJobsWithDiagnostics().Jobs;

        private static JobScanResult GetAllJobsWithDiagnostics()
        {
            var jobs = new List<PrintJob>();
            var diag = new StringBuilder();

            try
            {
                using var server = new LocalPrintServer();
                diag.AppendLine("LocalPrintServer: OK");

                PrintQueueCollection queues;
                try
                {
                    queues = server.GetPrintQueues(new[]
                    {
                        EnumeratedPrintQueueTypes.Local,
                        EnumeratedPrintQueueTypes.Connections,
                    });
                    diag.AppendLine("GetPrintQueues(Local+Connections): OK");
                }
                catch (Exception ex)
                {
                    diag.AppendLine($"GetPrintQueues FAILED: {ex.GetType().Name}: {ex.Message}");
                    return new(jobs, diag.ToString());
                }

                int queueCount = 0;
                foreach (PrintQueue queue in queues)
                {
                    queueCount++;
                    string name = queue.Name;
                    try
                    {
                        int jobCount = 0;
                        var collection = queue.GetPrintJobInfoCollection();
                        if (collection != null)
                        {
                            foreach (PrintSystemJobInfo job in collection)
                            {
                                if (job == null) continue;
                                jobCount++;
                                jobs.Add(new PrintJob
                                {
                                    JobId        = job.JobIdentifier,
                                    DocumentName = job.Name      ?? string.Empty,
                                    PrinterName  = name,
                                    Status       = FormatStatus(job.JobStatus),
                                    Owner        = job.Submitter ?? string.Empty,
                                    TotalPages   = job.NumberOfPages,
                                    SubmittedAt  = job.TimeJobSubmitted,
                                });
                            }
                        }
                        diag.AppendLine($"  [{name}] {jobCount} job(s)");
                    }
                    catch (Exception ex)
                    {
                        // System.Printing failed for this queue — fall back to Win32 EnumJobs
                        // which works even when the printer is in an error state.
                        int fallback = GetJobCountWin32(name);
                        for (int i = 0; i < fallback; i++)
                            jobs.Add(new PrintJob { PrinterName = name, DocumentName = "—", Status = "—" });

                        diag.AppendLine($"  [{name}] Win32 fallback: {fallback} job(s)  ({ex.GetType().Name}: {ex.Message})");
                    }
                    finally { queue.Dispose(); }
                }

                diag.AppendLine($"Queues scanned: {queueCount}  Total jobs: {jobs.Count}");
            }
            catch (Exception ex)
            {
                diag.AppendLine($"LocalPrintServer FAILED: {ex.GetType().Name}: {ex.Message}");
            }

            return new(jobs, diag.ToString());
        }

        // Win32 fallback — reliable even when System.Printing throws for an error-state printer.
        [DllImport("winspool.drv", SetLastError = true, CharSet = CharSet.Unicode)]
        private static extern bool OpenPrinter(string szPrinter, out IntPtr hPrinter, IntPtr pDefault);

        [DllImport("winspool.drv", SetLastError = true)]
        private static extern bool ClosePrinter(IntPtr hPrinter);

        [DllImport("winspool.drv", SetLastError = true)]
        private static extern bool EnumJobs(IntPtr hPrinter, int FirstJob, int NoJobs,
            int Level, IntPtr pJob, int cbBuf, out int pcbNeeded, out int pcReturned);

        [DllImport("winspool.drv", SetLastError = true)]
        private static extern bool SetJob(IntPtr hPrinter, int JobId, int Level,
            IntPtr pJob, int Command);

        private const int JOB_CONTROL_DELETE = 5;

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct JOB_INFO_1
        {
            public int    JobId;
            public IntPtr pPrinterName;
            public IntPtr pMachineName;
            public IntPtr pUserName;
            public IntPtr pDocument;
            public IntPtr pDatatype;
            public IntPtr pStatus;
            public int    Status;
            public int    Priority;
            public int    Position;
            public int    TotalPages;
            public int    PagesPrinted;
            public SYSTEMTIME Submitted;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct SYSTEMTIME
        {
            public short wYear, wMonth, wDayOfWeek, wDay;
            public short wHour, wMinute, wSecond, wMilliseconds;
        }

        private static int GetJobCountWin32(string printerName)
        {
            if (!OpenPrinter(printerName, out var hPrinter, IntPtr.Zero))
                return 0;
            try
            {
                // First call with zero buffer to obtain required size.
                EnumJobs(hPrinter, 0, int.MaxValue, 1, IntPtr.Zero, 0, out int needed, out _);
                if (needed == 0) return 0;

                var buf = Marshal.AllocHGlobal(needed);
                try
                {
                    return EnumJobs(hPrinter, 0, int.MaxValue, 1, buf, needed, out _, out int count)
                        ? count : 0;
                }
                finally { Marshal.FreeHGlobal(buf); }
            }
            catch { return 0; }
            finally  { ClosePrinter(hPrinter); }
        }

        private static void PurgeQueue(string printerName)
        {
            // Try System.Printing first (works if running as admin).
            try
            {
                using var server = new LocalPrintServer();
                using var queue  = server.GetPrintQueue(printerName);
                queue.Purge();
                return;
            }
            catch { }

            // Fall back to Win32 — cancel each job individually.
            // JOB_CONTROL_DELETE works for jobs the current user owns without admin rights.
            PurgeQueueWin32(printerName);
        }

        private static void PurgeQueueWin32(string printerName)
        {
            if (!OpenPrinter(printerName, out var hPrinter, IntPtr.Zero))
                throw new InvalidOperationException(
                    $"OpenPrinter failed (Win32 error {Marshal.GetLastWin32Error()})");
            try
            {
                EnumJobs(hPrinter, 0, int.MaxValue, 1, IntPtr.Zero, 0, out int needed, out _);
                if (needed == 0) return;

                var buf = Marshal.AllocHGlobal(needed);
                try
                {
                    if (!EnumJobs(hPrinter, 0, int.MaxValue, 1, buf, needed, out _, out int count))
                        return;

                    int stride = Marshal.SizeOf<JOB_INFO_1>();
                    for (int i = 0; i < count; i++)
                    {
                        var job = Marshal.PtrToStructure<JOB_INFO_1>(IntPtr.Add(buf, i * stride));
                        SetJob(hPrinter, job.JobId, 0, IntPtr.Zero, JOB_CONTROL_DELETE);
                    }
                }
                finally { Marshal.FreeHGlobal(buf); }
            }
            finally { ClosePrinter(hPrinter); }
        }

        private static string FormatStatus(PrintJobStatus status)
        {
            if (status == PrintJobStatus.None)               return "Queued";
            if (status.HasFlag(PrintJobStatus.Printing))     return "Printing";
            if (status.HasFlag(PrintJobStatus.Paused))       return "Paused";
            if (status.HasFlag(PrintJobStatus.Error))        return "Error";
            if (status.HasFlag(PrintJobStatus.Deleting))     return "Deleting";
            if (status.HasFlag(PrintJobStatus.Spooling))     return "Spooling";
            if (status.HasFlag(PrintJobStatus.Offline))      return "Offline";
            if (status.HasFlag(PrintJobStatus.PaperOut))     return "Paper Out";
            return status.ToString();
        }
    }
}
