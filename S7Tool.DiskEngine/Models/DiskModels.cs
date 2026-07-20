using CommunityToolkit.Mvvm.ComponentModel;

namespace S7Tool.DiskEngine.Models;

public partial class DiskInfo : ObservableObject
{
    public required int DiskNumber { get; init; }
    public required string Model { get; init; }
    public required long SizeBytes { get; init; }
    public string? BusType { get; init; }

    public required string PartitionStyle { get; set; }

    public List<DiskPartitionInfo> Partitions { get; } = new();

    public double SizeGb => Math.Round(SizeBytes / 1024.0 / 1024.0 / 1024.0, 1);

    public string DisplayName => string.IsNullOrWhiteSpace(BusType)
        ? $"Disque {DiskNumber} — {Model} ({SizeGb} Go, {PartitionStyle})"
        : $"Disque {DiskNumber} — {Model} ({SizeGb} Go, {BusType}, {PartitionStyle})";
}

public partial class DiskPartitionInfo : ObservableObject
{
    public required int DiskNumber { get; init; }

    public required int PartitionNumber { get; init; }

    public string? DriveLetter { get; init; }
    public required string PartitionType { get; init; }
    public required long SizeBytes { get; init; }
    public string? VolumeLabel { get; init; }
    public string? FileSystem { get; init; }
    public bool IsUnallocated { get; init; }

    public Guid? TypeGuid { get; init; }
    public Guid? UniqueGuid { get; init; }
    public bool IsHidden { get; init; }

    public long StartOffsetBytes { get; init; }
    public bool IsAligned => StartOffsetBytes % (1024 * 1024) == 0;

    public double SizeGb => Math.Round(SizeBytes / 1024.0 / 1024.0 / 1024.0, 1);
    public double StartOffsetGb => Math.Round(StartOffsetBytes / 1024.0 / 1024.0 / 1024.0, 3);

    public string DisplayName => IsUnallocated
        ? "Espace non alloué"
        : string.IsNullOrEmpty(DriveLetter)
            ? $"Partition {PartitionNumber} ({PartitionType})"
            : $"{DriveLetter}: {VolumeLabel}".Trim();

    public long MinSizeBytes { get; set; }
    public long MaxSizeBytes { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasPendingChange))]
    [NotifyPropertyChangedFor(nameof(BarLabel))]
    private double pendingSizeGb;

    public bool HasPendingChange => Math.Abs(PendingSizeGb - SizeGb) > 0.05;

    public string BarLabel => HasPendingChange
        ? $"{DisplayName} ({Math.Round(PendingSizeGb, 1)} Go)"
        : $"{DisplayName} ({SizeGb} Go)";
}

public class DiskHealthInfo
{
    public required int DiskNumber { get; init; }

    public required string HealthStatus { get; init; }
}
