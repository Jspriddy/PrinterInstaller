using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace HPPrinterScanner.Models
{
    public enum PrinterPortType { TcpIp, Ipp, Wsd, Local, Unknown }

    public class InstalledPrinter : INotifyPropertyChanged
    {
        public string Name       { get; init; } = string.Empty;
        public string PortName   { get; init; } = string.Empty;
        public string DriverName { get; init; } = string.Empty;
        public string IpAddress  { get; init; } = string.Empty;
        public PrinterPortType PortType { get; init; }

        private int _activeJobCount;
        public int ActiveJobCount
        {
            get => _activeJobCount;
            set
            {
                if (_activeJobCount == value) return;
                _activeJobCount = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(ActiveJobsLabel));
            }
        }

        public string ActiveJobsLabel => ActiveJobCount > 0 ? ActiveJobCount.ToString() : "—";

        private PrinterStatus _status = PrinterStatus.Unknown;
        public PrinterStatus Status
        {
            get => _status;
            set
            {
                if (_status == value) return;
                _status = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(StatusTooltip));
            }
        }

        public string StatusTooltip => Status switch
        {
            PrinterStatus.Online   => "Online — Responding to ping",
            PrinterStatus.Offline  => "Offline — No response",
            PrinterStatus.Sleeping => "Sleeping — Reachable but in low-power mode",
            PrinterStatus.Error    => "Error — Paper jam, toner issue, or error state",
            PrinterStatus.Printing => "Printing — Active jobs in queue",
            _                      => "Checking status…",
        };

        private bool _isDefault;
        public bool IsDefault
        {
            get => _isDefault;
            set
            {
                if (_isDefault == value) return;
                _isDefault = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(NameLabel));
            }
        }

        public string NameLabel => IsDefault ? $"{Name}  ✓" : Name;

        public string PortTypeLabel => PortType switch
        {
            PrinterPortType.TcpIp   => "TCP/IP",
            PrinterPortType.Ipp     => "IPP",
            PrinterPortType.Wsd     => "WSD",
            PrinterPortType.Local   => "Local",
            _                       => "Unknown",
        };

        public bool IsNetworkInstalled =>
            PortType == PrinterPortType.Ipp || PortType == PrinterPortType.Wsd;

        public event PropertyChangedEventHandler? PropertyChanged;
        private void OnPropertyChanged([CallerMemberName] string? name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
