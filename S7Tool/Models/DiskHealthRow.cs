using S7Tool.DiskEngine.Models;
using S7Tool.Services;

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
    public string FreeDisplay => FreeGb.HasValue ? $"{FreeGb}{LocalizationManager.T("Str_DiskHealth_FreeSuffix")}" : "N/A";
    public string CapacityDisplay => FreeGb.HasValue ? string.Format(LocalizationManager.T("Str_DiskHealth_UsedOfTotal"), UsedGb, SizeGb, FreeGb) : $"{SizeGb} Go";
    public string Model => string.IsNullOrWhiteSpace(Smart.Model) ? LocalizationManager.T("Str_DiskHealth_UnknownDisk") : Smart.Model;
    public string DisplayName => string.Format(LocalizationManager.T("Str_DiskHealth_DiskDisplayName"), DiskNumber, Model, SizeGb);
    public string HealthTier => Smart.HealthTier;
    public double? TemperatureCelsius => Smart.TemperatureCelsius;
    public string TemperatureDisplay => Smart.TemperatureCelsius.HasValue ? $"{Smart.TemperatureCelsius:0.#} °C" : "N/A";
    public double? WearPercentUsed => Smart.WearPercentUsed;
    public string WearDisplay => Smart.WearPercentUsed.HasValue ? $"{Smart.WearPercentUsed:0.#} %" : "N/A";
    public long? PowerOnHours => Smart.PowerOnHours;
    public string PowerOnHoursDisplay => Smart.PowerOnHours.HasValue ? $"{Smart.PowerOnHours} h" : "N/A";
}
