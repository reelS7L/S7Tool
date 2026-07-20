using System.Runtime.InteropServices;
using S7Tool.DiskEngine.Models;

namespace S7Tool.DiskEngine;

public static class AtaSmartReader
{
    private const int Timeout = 10;

    public static unsafe bool TryReadIdentify(PhysicalDiskAccessor accessor, out string? model, out string? firmware, out string? serial)
    {
        model = firmware = serial = null;

        if (!SendAtaCommand(accessor, NativeMethods.AtaCommandIdentify, feature: 0, lbaMid: 0, lbaHigh: 0, out byte[] data))
            return false;

        model = ReadAtaString(data, wordOffset: 27, wordCount: 20);
        firmware = ReadAtaString(data, wordOffset: 23, wordCount: 4);
        serial = ReadAtaString(data, wordOffset: 10, wordCount: 10);
        return true;
    }

    public static bool TryReadSmartAttributes(PhysicalDiskAccessor accessor, out List<SmartAttribute> attributes)
    {
        attributes = new List<SmartAttribute>();

        if (!SendAtaCommand(accessor, NativeMethods.AtaCommandSmart, NativeMethods.SmartFeatureReadData,
                NativeMethods.SmartLbaMid, NativeMethods.SmartLbaHigh, out byte[] valuesPage))
            return false;

        byte[]? thresholdsPage = null;
        if (SendAtaCommand(accessor, NativeMethods.AtaCommandSmart, NativeMethods.SmartFeatureReadThresholds,
                NativeMethods.SmartLbaMid, NativeMethods.SmartLbaHigh, out byte[] thresholds))
            thresholdsPage = thresholds;

        for (int i = 0; i < 30; i++)
        {
            int offset = 2 + i * 12;
            byte id = valuesPage[offset];
            if (id == 0) continue;

            byte current = valuesPage[offset + 3];
            byte worst = valuesPage[offset + 4];
            ulong raw = 0;
            for (int b = 0; b < 6; b++) raw |= (ulong)valuesPage[offset + 5 + b] << (b * 8);

            byte threshold = 0;
            if (thresholdsPage is not null)
            {
                int thOffset = 2 + i * 12;
                if (thresholdsPage[thOffset] == id)
                    threshold = thresholdsPage[thOffset + 1];
            }

            bool isCritical = SmartAttributeCatalog.CriticalIds.Contains(id)
                ? raw > 0
                : threshold > 0 && current <= threshold;

            attributes.Add(new SmartAttribute
            {
                Id = id,
                Name = SmartAttributeCatalog.GetName(id),
                Current = current,
                Worst = worst,
                Threshold = threshold,
                RawValue = raw,
                IsCritical = isCritical
            });
        }

        return attributes.Count > 0;
    }

    public static bool StartSelfTest(PhysicalDiskAccessor accessor, bool extended)
    {
        byte subcommand = extended ? (byte)0x02 : (byte)0x01;
        return SendNonDataAtaCommand(accessor, NativeMethods.AtaCommandSmart, feature: 0xD4, lbaLow: subcommand);
    }

    private static unsafe bool SendNonDataAtaCommand(PhysicalDiskAccessor accessor, byte command, byte feature, byte lbaLow)
    {
        int headerSize = sizeof(NativeMethods.AtaPassThroughEx);
        IntPtr buffer = Marshal.AllocHGlobal(headerSize);
        try
        {
            new Span<byte>((void*)buffer, headerSize).Clear();

            var header = (NativeMethods.AtaPassThroughEx*)buffer;
            header->Length = (ushort)headerSize;
            header->AtaFlags = NativeMethods.ATA_FLAGS_DRDY_REQUIRED;
            header->TimeOutValue = Timeout;

            header->CurrentTaskFile[0] = feature;
            header->CurrentTaskFile[1] = 1;
            header->CurrentTaskFile[2] = lbaLow;
            header->CurrentTaskFile[3] = NativeMethods.SmartLbaMid;
            header->CurrentTaskFile[4] = NativeMethods.SmartLbaHigh;
            header->CurrentTaskFile[5] = 0xA0;
            header->CurrentTaskFile[6] = command;

            return accessor.QueryDeviceIoControl(NativeMethods.IOCTL_ATA_PASS_THROUGH, buffer, (uint)headerSize, buffer, (uint)headerSize, out _);
        }
        finally
        {
            Marshal.FreeHGlobal(buffer);
        }
    }

    private static unsafe bool SendAtaCommand(PhysicalDiskAccessor accessor, byte command, byte feature, byte lbaMid, byte lbaHigh, out byte[] data)
    {
        data = Array.Empty<byte>();

        int headerSize = sizeof(NativeMethods.AtaPassThroughEx);
        const int dataSize = 512;
        int totalSize = headerSize + dataSize;

        IntPtr buffer = Marshal.AllocHGlobal(totalSize);
        try
        {
            new Span<byte>((void*)buffer, totalSize).Clear();

            var header = (NativeMethods.AtaPassThroughEx*)buffer;
            header->Length = (ushort)headerSize;
            header->AtaFlags = NativeMethods.ATA_FLAGS_DRDY_REQUIRED | NativeMethods.ATA_FLAGS_DATA_IN;
            header->DataTransferLength = dataSize;
            header->TimeOutValue = Timeout;
            header->DataBufferOffset = (IntPtr)headerSize;

            header->CurrentTaskFile[0] = feature;
            header->CurrentTaskFile[1] = 1;
            header->CurrentTaskFile[2] = 0;
            header->CurrentTaskFile[3] = lbaMid;
            header->CurrentTaskFile[4] = lbaHigh;
            header->CurrentTaskFile[5] = 0xA0;
            header->CurrentTaskFile[6] = command;

            bool ok = accessor.QueryDeviceIoControl(
                NativeMethods.IOCTL_ATA_PASS_THROUGH, buffer, (uint)totalSize, buffer, (uint)totalSize, out _);

            if (!ok) return false;

            data = new byte[dataSize];
            Marshal.Copy(IntPtr.Add(buffer, headerSize), data, 0, dataSize);
            return true;
        }
        finally
        {
            Marshal.FreeHGlobal(buffer);
        }
    }

    private static string ReadAtaString(byte[] data, int wordOffset, int wordCount)
    {
        var chars = new char[wordCount * 2];
        for (int i = 0; i < wordCount; i++)
        {
            int byteOffset = (wordOffset + i) * 2;
            chars[i * 2] = (char)data[byteOffset + 1];
            chars[i * 2 + 1] = (char)data[byteOffset];
        }
        return new string(chars).Trim().TrimEnd('\0').Trim();
    }
}
