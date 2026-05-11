using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;

namespace HPPrinterScanner.Models
{
    public class NetworkPrinter : INotifyPropertyChanged
    {
        private string _model  = string.Empty;
        private string _status = "Detected";

        public string IpAddress    { get; init; } = string.Empty;
        public string MacAddress   { get; init; } = string.Empty;
        public string Hostname     { get; init; } = string.Empty;
        public bool   Port9100Open { get; init; }
        public bool   Port80Open   { get; init; }
        public bool   Port631Open  { get; init; }

        public string Model
        {
            get => _model;
            set { _model = value; OnPropertyChanged(); }
        }

        public string Status
        {
            get => _status;
            set { _status = value; OnPropertyChanged(); }
        }

        public string Ports =>
            string.Join(", ", new[]
            {
                Port9100Open ? "9100" : null,
                Port80Open   ? "80"   : null,
                Port631Open  ? "631"  : null,
            }.Where(p => p != null)!);

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
