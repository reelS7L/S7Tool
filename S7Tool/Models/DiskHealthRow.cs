using S7Tool.DiskEngine.Models;

namespace S7Tool.Models;

public class DiskHealthRow
{
    public required int DiskNumber { get; init; }
    public required string BusType { get; init; }
    public required long SizeBytes { get; init; }
    public required string DriveLetters { get; init; }
    public required DiskSmartInfo Smart { get; init; }

    public long? FreeBytes { get; init; }

    public double SizeGb => Math.Round(SizeBytes / 1024.0 / 1024.0 / 1024.0, 1);
    public double? FreeGb => FreeBytes.HasValue ? Math.Round(FreeBytes.Value / 1024.0 / 1024.0 / 1024.0, 1) : null;
    public double? UsedGb => FreeBytes.HasValue ? Math.Round(SizeGb - FreeGb!.Value, 1) : null;
    public string FreeDisplay => FreeGb.HasValue ? $"{FreeGb} Go libres" : "N/A";
    public string CapacityDisplay => FreeGb.HasValue ? $"{UsedGb} Go utilisés / {SizeGb} Go ({FreeGb} Go libres)" : $"{SizeGb} Go";
    public string Model => string.IsNullOrWhiteSpace(Smart.Model) ? "Disque inconnu" : Smart.Model;
    public string DisplayName => $"Disque {DiskNumber} — {Model} ({SizeGb} Go)";
    public string HealthTier => Smart.HealthTier;
    public double? TemperatureCelsius => Smart.TemperatureCelsius;
    public string TemperatureDisplay => Smart.TemperatureCelsius.HasValue ? $"{Smart.TemperatureCelsius:0.#} °C" : "N/A";
    public double? WearPercentUsed => Smart.WearPercentUsed;
    public string WearDisplay => Smart.WearPercentUsed.HasValue ? $"{Smart.WearPercentUsed:0.#} %" : "N/A";
    public long? PowerOnHours => Smart.PowerOnHours;
    public string PowerOnHoursDisplay => Smart.PowerOnHours.HasValue ? $"{Smart.PowerOnHours} h" : "N/A";
}
