using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace S7Tool.DiskEngine;

public sealed class PhysicalDiskAccessor : IDisposable
{
    public const int DefaultSectorSize = 512;

    private readonly SafeFileHandle _handle;
    public int SectorSize { get; }
    public long DiskSizeBytes { get; }

    private PhysicalDiskAccessor(SafeFileHandle handle, int sectorSize, long diskSizeBytes)
    {
        _handle = handle;
        SectorSize = sectorSize;
        DiskSizeBytes = diskSizeBytes;
    }

    public static PhysicalDiskAccessor OpenForRead(int diskNumber) => Open(diskNumber, write: false);
    public static PhysicalDiskAccessor OpenForWrite(int diskNumber) => Open(diskNumber, write: true);

    private static PhysicalDiskAccessor Open(int diskNumber, bool write)
    {
        string path = $@"\\.\PhysicalDrive{diskNumber}";
        uint access = write ? (NativeMethods.GENERIC_READ | NativeMethods.GENERIC_WRITE) : NativeMethods.GENERIC_READ;
        uint flags = NativeMethods.FILE_FLAG_NO_BUFFERING | NativeMethods.FILE_FLAG_SEQUENTIAL_SCAN;
        if (write) flags |= NativeMethods.FILE_FLAG_WRITE_THROUGH;

        var handle = NativeMethods.CreateFileW(
            path, access, NativeMethods.FILE_SHARE_READ | NativeMethods.FILE_SHARE_WRITE,
            IntPtr.Zero, NativeMethods.OPEN_EXISTING, flags, IntPtr.Zero);

        if (handle.IsInvalid)
        {
            int err = Marshal.GetLastWin32Error();
            handle.Dispose();
            throw new IOException(
                $"Impossible d'ouvrir {path} (erreur Windows {err}). L'application doit tourner en " +
                "administrateur, et aucun volume du disque ne doit être encore monté/verrouillé par un autre processus.");
        }

        var (sectorSize, diskSize) = QueryGeometry(handle);
        return new PhysicalDiskAccessor(handle, sectorSize, diskSize);
    }

    private static unsafe (int SectorSize, long DiskSize) QueryGeometry(SafeFileHandle handle)
    {
        const int bufferSize = 64;
        IntPtr buffer = Marshal.AllocHGlobal(bufferSize);
        try
        {
            bool ok = NativeMethods.DeviceIoControl(
                handle, NativeMethods.IOCTL_DISK_GET_DRIVE_GEOMETRY_EX,
                IntPtr.Zero, 0, buffer, bufferSize, out _, IntPtr.Zero);

            if (!ok) return (DefaultSectorSize, 0);

            int bytesPerSector = Marshal.ReadInt32(buffer, 20);
            long diskSize = Marshal.ReadInt64(buffer, 24);
            return (bytesPerSector > 0 ? bytesPerSector : DefaultSectorSize, diskSize);
        }
        finally
        {
            Marshal.FreeHGlobal(buffer);
        }
    }

    public unsafe void ReadAt(long offset, Span<byte> buffer)
    {
        Seek(offset);
        fixed (byte* p = buffer)
        {
            if (!NativeMethods.ReadFile(_handle, p, (uint)buffer.Length, out uint read, IntPtr.Zero) || read != buffer.Length)
                throw new IOException($"Échec de lecture à l'offset {offset:N0} (erreur Windows {Marshal.GetLastWin32Error()}).");
        }
    }

    public unsafe void WriteAt(long offset, ReadOnlySpan<byte> buffer)
    {
        Seek(offset);
        fixed (byte* p = buffer)
        {
            if (!NativeMethods.WriteFile(_handle, p, (uint)buffer.Length, out uint written, IntPtr.Zero) || written != buffer.Length)
                throw new IOException($"Échec d'écriture à l'offset {offset:N0} (erreur Windows {Marshal.GetLastWin32Error()}).");
        }
    }

    private void Seek(long offset)
    {
        if (!NativeMethods.SetFilePointerEx(_handle, offset, out _, 0))
            throw new IOException($"Échec de positionnement à l'offset {offset:N0} (erreur Windows {Marshal.GetLastWin32Error()}).");
    }

    public bool QueryDeviceIoControl(uint ioControlCode, IntPtr inBuffer, uint inBufferSize, IntPtr outBuffer, uint outBufferSize, out uint bytesReturned) =>
        NativeMethods.DeviceIoControl(_handle, ioControlCode, inBuffer, inBufferSize, outBuffer, outBufferSize, out bytesReturned, IntPtr.Zero);

    public void Dispose() => _handle.Dispose();
}
