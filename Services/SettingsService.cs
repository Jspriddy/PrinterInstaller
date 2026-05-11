using System;
using System.IO;
using System.Text.Json;
using HPPrinterScanner.Models;

namespace HPPrinterScanner.Services
{
    public class SettingsService
    {
        // FilePath and JsonOpts MUST be declared before Instance — static field
        // initializers run top-to-bottom, and Instance's constructor calls Load()
        // which reads FilePath. If FilePath were declared after Instance it would
        // still be null when Load() ran.
        public static readonly string FilePath = Path.Combine(
            LocateAppData(), "HPPrinterScanner", "settings.json");

        private static readonly JsonSerializerOptions JsonOpts = new()
        {
            WriteIndented               = true,
            PropertyNameCaseInsensitive = true,
        };

        public static readonly SettingsService Instance = new();

        private static string LocateAppData()
        {
            var via1 = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            if (!string.IsNullOrWhiteSpace(via1)) return via1;

            var via2 = Environment.GetEnvironmentVariable("APPDATA");
            if (!string.IsNullOrWhiteSpace(via2)) return via2;

            var via3 = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            if (!string.IsNullOrWhiteSpace(via3)) return via3;

            return AppContext.BaseDirectory;
        }

        public AppSettings Current { get; private set; } = new();
        public string? LastLoadError { get; private set; }

        private SettingsService() => Load();

        public void Load()
        {
            LastLoadError = null;
            try
            {
                if (!File.Exists(FilePath))
                {
                    LastLoadError = $"First run — no settings file yet: {FilePath}";
                    return;
                }
                Current = JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(FilePath), JsonOpts) ?? new();
            }
            catch (Exception ex) { LastLoadError = ex.Message; Current = new(); }
        }

        public void Save()
        {
            Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
            File.WriteAllText(FilePath, JsonSerializer.Serialize(Current, JsonOpts));
        }

        public string EffectiveSubnet()
            => string.IsNullOrWhiteSpace(Current.SubnetOverride)
                ? NetworkScannerService.DetectLocalSubnet()
                : Current.SubnetOverride;
    }
}
