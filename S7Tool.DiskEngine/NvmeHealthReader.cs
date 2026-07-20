using System.Runtime.InteropServices;

namespace S7Tool.DiskEngine;

public readonly record struct NvmeHealthLog(
    double TemperatureCelsius,
    double AvailableSparePercent,
    double PercentageUsed,
    long PowerOnHours,
    long UnsafeShutdowns,
    long MediaErrors,
    ulong DataUnitsRead,
    ulong DataUnitsWritten);

public static class NvmeHealthReader
{
    private const int StorageDeviceProtocolSpecificProperty = 50;
    private const int ProtocolTypeNvme = 3;
    private const int NvmeDataTypeLogPage = 2;
    private const int NvmeHealthLogPageId = 0x02;

    public static unsafe bool TryRead(PhysicalDiskAccessor accessor, out NvmeHealthLog health)
    {
        health = default;

        const int requestSize = 8 + 40;
        const int logPageSize = 512;
        const int responseSize = 48 + logPageSize;

        IntPtr inBuffer = Marshal.AllocHGlobal(requestSize);
        IntPtr outBuffer = Marshal.AllocHGlobal(responseSize);
        try
        {
            new Span<byte>((void*)inBuffer, requestSize).Clear();
            new Span<byte>((void*)outBuffer, responseSize).Clear();

            Marshal.WriteInt32(inBuffer, 0, StorageDeviceProtocolSpecificProperty);
            Marshal.WriteInt32(inBuffer, 4, 0);
            Marshal.WriteInt32(inBuffer, 8, ProtocolTypeNvme);
            Marshal.WriteInt32(inBuffer, 12, NvmeDataTypeLogPage);
            Marshal.WriteInt32(inBuffer, 16, NvmeHealthLogPageId);
            Marshal.WriteInt32(inBuffer, 20, 0);
            Marshal.WriteInt32(inBuffer, 24, 0);
            Marshal.WriteInt32(inBuffer, 28, logPageSize);

            bool ok = accessor.QueryDeviceIoControl(
                NativeMethods.IOCTL_STORAGE_QUERY_PROPERTY, inBuffer, requestSize, outBuffer, responseSize, out _);
            if (!ok) return false;

            var page = new Span<byte>((void*)IntPtr.Add(outBuffer, 48), logPageSize);

            ushort tempKelvin = BitConverter.ToUInt16(page.Slice(1, 2));
            double tempCelsius = tempKelvin > 0 ? tempKelvin - 273.15 : 0;

            health = new NvmeHealthLog(
                TemperatureCelsius: tempCelsius,
                AvailableSparePercent: page[3],
                PercentageUsed: page[5],
                PowerOnHours: (long)Read128Low64(page, 128),
                UnsafeShutdowns: (long)Read128Low64(page, 144),
                MediaErrors: (long)Read128Low64(page, 160),
                DataUnitsRead: Read128Low64(page, 32),
                DataUnitsWritten: Read128Low64(page, 48));

            return true;
        }
        finally
        {
            Marshal.FreeHGlobal(inBuffer);
            Marshal.FreeHGlobal(outBuffer);
        }
    }

    private static ulong Read128Low64(ReadOnlySpan<byte> page, int offset) => BitConverter.ToUInt64(page.Slice(offset, 8));
}
