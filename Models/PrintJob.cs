using System;

namespace HPPrinterScanner.Models
{
    public class PrintJob
    {
        public int      JobId        { get; init; }
        public string   DocumentName { get; init; } = string.Empty;
        public string   PrinterName  { get; init; } = string.Empty;
        public string   Status       { get; init; } = string.Empty;
        public string   Owner        { get; init; } = string.Empty;
        public int      TotalPages   { get; init; }
        public DateTime SubmittedAt  { get; init; }

        public string PagesLabel     => TotalPages > 0 ? TotalPages.ToString() : "—";
        public string SubmittedLabel => SubmittedAt == default ? string.Empty : SubmittedAt.ToString("g");
    }
}
