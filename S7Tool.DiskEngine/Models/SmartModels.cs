namespace S7Tool.DiskEngine.Models;

public class SmartAttribute
{
    public required byte Id { get; init; }
    public required string Name { get; init; }
    public byte Current { get; init; }
    public byte Worst { get; init; }
    public byte Threshold { get; init; }
    public required ulong RawValue { get; init; }

    public bool IsCritical { get; init; }

    public string RawDisplay => Id is 0xC2 or 0xBE ? $"{RawValue & 0xFF} °C" : RawValue.ToString();
}

public class DiskSmartInfo
{
    public required int DiskNumber { get; init; }
    public string? Model { get; init; }
    public string? FirmwareRevision { get; init; }
    public string? SerialNumber { get; init; }

    public required string HealthTier { get; init; }

    public double? TemperatureCelsius { get; init; }
    public double? WearPercentUsed { get; init; }
    public long? PowerOnHours { get; init; }

    public List<SmartAttribute> Attributes { get; } = new();

    public double? AvailableSparePercent { get; init; }
    public long? UnsafeShutdowns { get; init; }
    public long? MediaErrors { get; init; }
    public ulong? DataUnitsRead { get; init; }
    public ulong? DataUnitsWritten { get; init; }
}
