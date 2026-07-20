using S7Tool.DiskEngine;
using S7Tool.DiskEngine.Models;
using S7Tool.Models;
using S7Tool.Services.Interfaces;
using System.IO;
using System.Text;
using System.Text.Json;

namespace S7Tool.Services;

public class DiskHealthService : IDiskHealthService
{
    private static readonly string DataDir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "S7Tool");
    private static readonly string HistoryPath = Path.Combine(DataDir, "smart-history.json");
    private static readonly string ThresholdsPath = Path.Combine(DataDir, "smart-thresholds.json");

    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    public Task<List<DiskHealthRow>> GetAllAsync() => Task.Run(() =>
    {
        var disks = DiskEnumerator.GetDisks();
        return disks.Select(d =>
        {
            var letters = d.Partitions.Where(p => !string.IsNullOrEmpty(p.DriveLetter)).Select(p => p.DriveLetter!).ToList();
            return new DiskHealthRow
            {
                DiskNumber = d.DiskNumber,
                BusType = d.BusType ?? LocalizationManager.T("Str_DiskHealth_UnknownBus"),
                SizeBytes = d.SizeBytes,
                DriveLetters = string.Join(", ", letters.Select(l => l + ":")),
                FreeBytes = letters.Count > 0 ? GetTotalFreeSpace(letters) : null,
                Smart = DiskEnumerator.GetSmartData(d.DiskNumber)
            };
        }).ToList();
    });

    private static long? GetTotalFreeSpace(List<string> driveLetters)
    {
        long total = 0;
        bool any = false;
        foreach (var letter in driveLetters)
        {
            try
            {
                var drive = new DriveInfo(letter);
                if (!drive.IsReady) continue;
                total += drive.TotalFreeSpace;
                any = true;
            }
            catch (IOException)
            {
            }
        }
        return any ? total : null;
    }

    public Task<bool> StartSelfTestAsync(int diskNumber, bool extended) => Task.Run(() =>
    {
        try
        {
            using var accessor = PhysicalDiskAccessor.OpenForWrite(diskNumber);
            return AtaSmartReader.StartSelfTest(accessor, extended);
        }
        catch (IOException)
        {
            return false;
        }
    });

    public async Task AppendHistoryAsync(IEnumerable<DiskSmartInfo> snapshot)
    {
        Directory.CreateDirectory(DataDir);

        var history = await LoadHistoryAsync();
        var now = DateTime.UtcNow;

        foreach (var disk in snapshot)
            history.Add(new SmartHistoryPoint(now, disk.DiskNumber, disk.TemperatureCelsius, disk.HealthTier));

        const int maxPointsPerDisk = 2000;
        var trimmed = history
            .GroupBy(p => p.DiskNumber)
            .SelectMany(g => g.OrderByDescending(p => p.TimestampUtc).Take(maxPointsPerDisk))
            .OrderBy(p => p.TimestampUtc)
            .ToList();

        await File.WriteAllTextAsync(HistoryPath, JsonSerializer.Serialize(trimmed, JsonOptions));
    }

    public async Task<List<SmartHistoryPoint>> GetHistoryAsync(int diskNumber, int maxPoints = 200)
    {
        var history = await LoadHistoryAsync();
        return history
            .Where(p => p.DiskNumber == diskNumber)
            .OrderBy(p => p.TimestampUtc)
            .TakeLast(maxPoints)
            .ToList();
    }

    private static async Task<List<SmartHistoryPoint>> LoadHistoryAsync()
    {
        if (!File.Exists(HistoryPath)) return new List<SmartHistoryPoint>();

        try
        {
            string json = await File.ReadAllTextAsync(HistoryPath);
            return JsonSerializer.Deserialize<List<SmartHistoryPoint>>(json) ?? new();
        }
        catch (JsonException)
        {
            return new List<SmartHistoryPoint>();
        }
    }

    public AlertThresholds GetAlertThresholds()
    {
        if (!File.Exists(ThresholdsPath))
            return new AlertThresholds(MaxTemperatureCelsius: 55, MinWearPercentRemaining: 10);

        try
        {
            string json = File.ReadAllText(ThresholdsPath);
            return JsonSerializer.Deserialize<AlertThresholds>(json) ?? new AlertThresholds(55, 10);
        }
        catch (JsonException)
        {
            return new AlertThresholds(55, 10);
        }
    }

    public void SetAlertThresholds(AlertThresholds thresholds)
    {
        Directory.CreateDirectory(DataDir);
        File.WriteAllText(ThresholdsPath, JsonSerializer.Serialize(thresholds, JsonOptions));
    }

    public async Task ExportAsync(IEnumerable<DiskSmartInfo> disks, string format, string destinationPath)
    {
        var list = disks.ToList();

        if (format.Equals("CSV", StringComparison.OrdinalIgnoreCase))
        {
            Helpers.CsvExporter.Export(destinationPath,
                new[] { "Disque", "Modele", "NumeroSerie", "Firmware", "Sante", "TemperatureC", "UsurePourcent", "HeuresFonctionnement", "AttributId", "AttributNom", "Actuel", "Pire", "Seuil", "Brut", "Critique" },
                list.SelectMany(d => d.Attributes.Count == 0
                    ? new[] { BuildCsvRow(d, null) }
                    : d.Attributes.Select(a => BuildCsvRow(d, a))));
            return;
        }

        string content = format.ToUpperInvariant() switch
        {
            "JSON" => JsonSerializer.Serialize(list, JsonOptions),
            _ => BuildText(list)
        };
        await File.WriteAllTextAsync(destinationPath, content, Encoding.UTF8);
    }

    private static IEnumerable<string?> BuildCsvRow(DiskSmartInfo d, SmartAttribute? a) => new[]
    {
        d.DiskNumber.ToString(), d.Model, d.SerialNumber, d.FirmwareRevision, d.HealthTier,
        d.TemperatureCelsius?.ToString(), d.WearPercentUsed?.ToString(), d.PowerOnHours?.ToString(),
        a is null ? null : $"0x{a.Id:X2}", a?.Name, a?.Current.ToString(), a?.Worst.ToString(), a?.Threshold.ToString(), a?.RawValue.ToString(), a?.IsCritical.ToString()
    };

    private static string BuildText(List<DiskSmartInfo> disks)
    {
        var sb = new StringBuilder();
        foreach (var d in disks)
        {
            sb.AppendLine($"=== Disque {d.DiskNumber} : {d.Model} ===");
            sb.AppendLine($"  Numéro de série : {d.SerialNumber}");
            sb.AppendLine($"  Firmware : {d.FirmwareRevision}");
            sb.AppendLine($"  État de santé : {d.HealthTier}");
            sb.AppendLine($"  Température : {d.TemperatureCelsius} °C");
            sb.AppendLine($"  Usure : {d.WearPercentUsed} %");
            sb.AppendLine($"  Heures de fonctionnement : {d.PowerOnHours}");
            if (d.Attributes.Count > 0)
            {
                sb.AppendLine("  Attributs SMART :");
                foreach (var a in d.Attributes)
                    sb.AppendLine($"    [0x{a.Id:X2}] {a.Name} : actuel={a.Current} pire={a.Worst} seuil={a.Threshold} brut={a.RawDisplay}{(a.IsCritical ? " (CRITIQUE)" : "")}");
            }
            sb.AppendLine();
        }
        return sb.ToString();
    }
}
