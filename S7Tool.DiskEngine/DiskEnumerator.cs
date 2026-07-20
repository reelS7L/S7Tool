using System.Runtime.InteropServices;
using System.Text;
using S7Tool.DiskEngine.Models;

namespace S7Tool.DiskEngine;

public static class DiskEnumerator
{
    private static readonly Dictionary<Guid, string> KnownGptTypes = new()
    {
        [Guid.Parse("c12a7328-f81f-11d2-ba4b-00a0c93ec93b")] = "EFI",
        [Guid.Parse("e3c9e316-0b5c-4db8-817d-f92df00215ae")] = "Réservée (MSR)",
        [Guid.Parse("ebd0a0a2-b9e5-4433-87c0-68b6b72699c7")] = "Données basiques",
        [Guid.Parse("de94bba4-06d1-4d40-a16a-bfd50179d6ac")] = "Récupération",
    };

    private const long MinGapBytes = 1024 * 1024;

    public static List<DiskInfo> GetDisks()
    {
        var driveLetters = MapDriveLettersToPartitions();
        var disks = new List<DiskInfo>();

        for (int diskNumber = 0; diskNumber < 32; diskNumber++)
        {
            PhysicalDiskAccessor? accessor;
            try
            {
                accessor = PhysicalDiskAccessor.OpenForRead(diskNumber);
            }
            catch (IOException)
            {
                continue;
            }

            using (accessor)
            {
                try
                {
                    var (busType, model) = QueryDeviceProperty(accessor);

                    var disk = new DiskInfo
                    {
                        DiskNumber = diskNumber,
                        Model = string.IsNullOrWhiteSpace(model) ? "Disque inconnu" : model,
                        SizeBytes = accessor.DiskSizeBytes,
                        BusType = busType,
                        PartitionStyle = "RAW"
                    };

                    var built = new List<DiskPartitionInfo>();
                    try
                    {
                        var editor = new GptEditor(accessor);
                        var header = editor.ReadPrimaryHeader();
                        disk.PartitionStyle = "GPT";

                        int partitionNumber = 0;
                        foreach (var entry in editor.ReadEntries(header))
                        {
                            if (entry.IsEmpty) continue;
                            partitionNumber++;

                            driveLetters.TryGetValue((diskNumber, partitionNumber), out var vol);

                            built.Add(new DiskPartitionInfo
                            {
                                DiskNumber = diskNumber,
                                PartitionNumber = partitionNumber,
                                DriveLetter = vol.Letter,
                                PartitionType = KnownGptTypes.TryGetValue(entry.TypeGuid, out var typeName) ? typeName : entry.TypeGuid.ToString(),
                                SizeBytes = (long)((entry.LastLba - entry.FirstLba + 1) * (ulong)accessor.SectorSize),
                                VolumeLabel = vol.Label ?? entry.Name,
                                FileSystem = vol.FileSystem,
                                TypeGuid = entry.TypeGuid,
                                UniqueGuid = entry.UniqueGuid,
                                IsHidden = (entry.Attributes & 0x4000000000000000) != 0,
                                StartOffsetBytes = (long)(entry.FirstLba * (ulong)accessor.SectorSize),
                            });
                        }
                    }
                    catch (InvalidOperationException)
                    {
                    }

                    disk.Partitions.AddRange(WithUnallocatedGaps(built, disk.SizeBytes, diskNumber, accessor.SectorSize));
                    disks.Add(disk);
                }
                catch (IOException)
                {
                }
            }
        }

        return disks;
    }

    public static List<DiskHealthInfo> GetHealth(IEnumerable<int> diskNumbers)
    {
        var result = new List<DiskHealthInfo>();
        foreach (int diskNumber in diskNumbers)
        {
            string status = "Inconnu";
            try
            {
                using var accessor = PhysicalDiskAccessor.OpenForRead(diskNumber);
                int size = 4;
                IntPtr buffer = Marshal.AllocHGlobal(size);
                try
                {
                    bool ok = accessor.QueryDeviceIoControl(
                        NativeMethods.IOCTL_STORAGE_PREDICT_FAILURE, IntPtr.Zero, 0, buffer, (uint)size, out _);
                    if (ok)
                    {
                        bool predictedFailure = Marshal.ReadByte(buffer, 0) != 0;
                        status = predictedFailure ? "Attention" : "OK";
                    }
                }
                finally
                {
                    Marshal.FreeHGlobal(buffer);
                }
            }
            catch (IOException)
            {
            }

            result.Add(new DiskHealthInfo { DiskNumber = diskNumber, HealthStatus = status });
        }
        return result;
    }

    public static DiskSmartInfo GetSmartData(int diskNumber)
    {
        try
        {
            using var accessor = PhysicalDiskAccessor.OpenForWrite(diskNumber);
            var (busType, model) = QueryDeviceProperty(accessor);

            if (string.Equals(busType, "NVMe", StringComparison.OrdinalIgnoreCase))
                return BuildNvmeInfo(diskNumber, model, accessor);

            return BuildAtaInfo(diskNumber, model, accessor);
        }
        catch (IOException)
        {
            return new DiskSmartInfo { DiskNumber = diskNumber, HealthTier = "Inconnu" };
        }
    }

    private static DiskSmartInfo BuildNvmeInfo(int diskNumber, string? model, PhysicalDiskAccessor accessor)
    {
        if (!NvmeHealthReader.TryRead(accessor, out var health))
            return new DiskSmartInfo { DiskNumber = diskNumber, Model = model, HealthTier = "Inconnu" };

        string tier = health.PercentageUsed switch
        {
            >= 100 => "Critique",
            >= 90 => "Prudence",
            >= 70 => "Bon",
            _ => "Excellent"
        };
        if (health.AvailableSparePercent < 10) tier = "Critique";

        return new DiskSmartInfo
        {
            DiskNumber = diskNumber,
            Model = model,
            HealthTier = tier,
            TemperatureCelsius = health.TemperatureCelsius,
            WearPercentUsed = health.PercentageUsed,
            PowerOnHours = health.PowerOnHours,
            AvailableSparePercent = health.AvailableSparePercent,
            UnsafeShutdowns = health.UnsafeShutdowns,
            MediaErrors = health.MediaErrors,
            DataUnitsRead = health.DataUnitsRead,
            DataUnitsWritten = health.DataUnitsWritten
        };
    }

    private static DiskSmartInfo BuildAtaInfo(int diskNumber, string? model, PhysicalDiskAccessor accessor)
    {
        AtaSmartReader.TryReadIdentify(accessor, out string? identifyModel, out string? firmware, out string? serial);
        bool hasAttributes = AtaSmartReader.TryReadSmartAttributes(accessor, out var attributes);

        string tier = "Inconnu";
        long? powerOnHours = null;
        double? wearPercent = null;
        double? temperatureCelsius = null;

        if (hasAttributes)
        {
            bool anyCritical = attributes.Any(a => a.IsCritical);
            bool anyLowMargin = attributes.Any(a => a.Threshold > 0 && a.Current - a.Threshold < 10 && a.Current > a.Threshold);
            tier = anyCritical ? "Critique" : anyLowMargin ? "Prudence" : "Excellent";

            var poh = attributes.FirstOrDefault(a => a.Id == 0x09);
            if (poh is not null) powerOnHours = (long)poh.RawValue;

            var wear = attributes.FirstOrDefault(a => a.Id == 0xE8) ?? attributes.FirstOrDefault(a => a.Id == 0xB1);
            if (wear is not null) wearPercent = wear.Id == 0xE8 ? wear.Current : 100 - wear.Current;

            var temp = attributes.FirstOrDefault(a => a.Id == 0xC2) ?? attributes.FirstOrDefault(a => a.Id == 0xBE);
            if (temp is not null) temperatureCelsius = temp.RawValue & 0xFF;
        }

        var info = new DiskSmartInfo
        {
            DiskNumber = diskNumber,
            Model = string.IsNullOrWhiteSpace(identifyModel) ? model : identifyModel,
            FirmwareRevision = firmware,
            SerialNumber = serial,
            HealthTier = tier,
            PowerOnHours = powerOnHours,
            WearPercentUsed = wearPercent,
            TemperatureCelsius = temperatureCelsius
        };
        if (hasAttributes) info.Attributes.AddRange(attributes);
        return info;
    }

    private static List<DiskPartitionInfo> WithUnallocatedGaps(List<DiskPartitionInfo> partitions, long diskSizeBytes, int diskNumber, int sectorSize)
    {
        var ordered = partitions.OrderBy(p => p.StartOffsetBytes).ToList();
        var withGaps = new List<DiskPartitionInfo>();
        long cursor = 0;

        long usableStart = 1024 * 1024;
        long usableEnd = diskSizeBytes - 33L * sectorSize;

        foreach (var part in ordered)
        {
            long gapStart = Math.Max(cursor, usableStart);
            long gap = part.StartOffsetBytes - gapStart;
            if (gap > MinGapBytes)
            {
                withGaps.Add(new DiskPartitionInfo
                {
                    DiskNumber = diskNumber,
                    PartitionNumber = -1,
                    PartitionType = "Non alloué",
                    SizeBytes = gap,
                    StartOffsetBytes = gapStart,
                    IsUnallocated = true
                });
            }
            withGaps.Add(part);
            cursor = part.StartOffsetBytes + part.SizeBytes;
        }

        long trailingStart = Math.Max(cursor, usableStart);
        long trailingGap = usableEnd - trailingStart;
        if (trailingGap > MinGapBytes)
        {
            withGaps.Add(new DiskPartitionInfo
            {
                DiskNumber = diskNumber,
                PartitionNumber = -1,
                PartitionType = "Non alloué",
                SizeBytes = trailingGap,
                StartOffsetBytes = trailingStart,
                IsUnallocated = true
            });
        }

        return withGaps;
    }

    private static unsafe (string? BusType, string? Model) QueryDeviceProperty(PhysicalDiskAccessor accessor)
    {
        IntPtr inBuffer = Marshal.AllocHGlobal(12);
        const int outSize = 1024;
        IntPtr outBuffer = Marshal.AllocHGlobal(outSize);
        try
        {
            Marshal.WriteInt32(inBuffer, 0, 0);
            Marshal.WriteInt32(inBuffer, 4, 0);
            Marshal.WriteByte(inBuffer, 8, 0);

            bool ok = accessor.QueryDeviceIoControl(
                NativeMethods.IOCTL_STORAGE_QUERY_PROPERTY, inBuffer, 12, outBuffer, outSize, out _);
            if (!ok) return (null, null);

            int vendorIdOffset = Marshal.ReadInt32(outBuffer, 12);
            int productIdOffset = Marshal.ReadInt32(outBuffer, 16);
            int busTypeRaw = Marshal.ReadInt32(outBuffer, 28);

            string? vendor = ReadAnsiStringAt(outBuffer, vendorIdOffset, outSize);
            string? product = ReadAnsiStringAt(outBuffer, productIdOffset, outSize);
            string model = string.Join(" ", new[] { vendor, product }.Where(s => !string.IsNullOrWhiteSpace(s))).Trim();

            return (DescribeBusType(busTypeRaw), string.IsNullOrWhiteSpace(model) ? null : model);
        }
        finally
        {
            Marshal.FreeHGlobal(inBuffer);
            Marshal.FreeHGlobal(outBuffer);
        }
    }

    private static string? ReadAnsiStringAt(IntPtr buffer, int offset, int bufferSize)
    {
        if (offset <= 0 || offset >= bufferSize) return null;
        string s = Marshal.PtrToStringAnsi(IntPtr.Add(buffer, offset)) ?? "";
        return string.IsNullOrWhiteSpace(s) ? null : s.Trim();
    }

    private static string DescribeBusType(int busType) => busType switch
    {
        7 => "USB",
        11 => "SATA",
        3 => "ATA",
        1 => "SCSI",
        10 => "SAS",
        8 => "RAID",
        17 => "NVMe",
        12 => "SD",
        13 => "MMC",
        _ => "Disque"
    };

    private static Dictionary<(int Disk, int Partition), (string Letter, string? Label, string? FileSystem)> MapDriveLettersToPartitions()
    {
        var map = new Dictionary<(int, int), (string, string?, string?)>();

        for (char letter = 'C'; letter <= 'Z'; letter++)
        {
            string root = $@"{letter}:\";
            string devicePath = $@"\\.\{letter}:";

            var handle = NativeMethods.CreateFileW(
                devicePath, 0, NativeMethods.FILE_SHARE_READ | NativeMethods.FILE_SHARE_WRITE,
                IntPtr.Zero, NativeMethods.OPEN_EXISTING, 0, IntPtr.Zero);

            if (handle.IsInvalid) { handle.Dispose(); continue; }

            try
            {
                const int size = 12;
                IntPtr buffer = Marshal.AllocHGlobal(size);
                try
                {
                    bool ok = NativeMethods.DeviceIoControl(
                        handle, NativeMethods.IOCTL_STORAGE_GET_DEVICE_NUMBER, IntPtr.Zero, 0, buffer, size, out _, IntPtr.Zero);
                    if (!ok) continue;

                    int diskNumber = Marshal.ReadInt32(buffer, 4);
                    int partitionNumber = Marshal.ReadInt32(buffer, 8);
                    if (partitionNumber <= 0) continue;

                    var volLabel = new StringBuilder(256);
                    var fsName = new StringBuilder(64);
                    string? label = null, fileSystem = null;
                    if (NativeMethods.GetVolumeInformationW(root, volLabel, 256, out _, out _, out _, fsName, 64))
                    {
                        label = volLabel.ToString();
                        fileSystem = fsName.ToString();
                    }

                    map[(diskNumber, partitionNumber)] = (letter.ToString(), string.IsNullOrEmpty(label) ? null : label, string.IsNullOrEmpty(fileSystem) ? null : fileSystem);
                }
                finally
                {
                    Marshal.FreeHGlobal(buffer);
                }
            }
            finally
            {
                handle.Dispose();
            }
        }

        return map;
    }
}
