using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace S7Tool.DiskEngine;

internal static class NativeMethods
{
    public const uint GENERIC_READ = 0x80000000;
    public const uint GENERIC_WRITE = 0x40000000;
    public const uint FILE_SHARE_READ = 0x1;
    public const uint FILE_SHARE_WRITE = 0x2;
    public const uint OPEN_EXISTING = 3;
    public const uint FILE_FLAG_NO_BUFFERING = 0x20000000;
    public const uint FILE_FLAG_WRITE_THROUGH = 0x80000000;
    public const uint FILE_FLAG_SEQUENTIAL_SCAN = 0x08000000;

    public const uint FSCTL_LOCK_VOLUME = 0x00090018;
    public const uint FSCTL_DISMOUNT_VOLUME = 0x00090020;
    public const uint FSCTL_UNLOCK_VOLUME = 0x0009001C;
    public const uint IOCTL_DISK_GET_DRIVE_GEOMETRY_EX = 0x000700A0;
    public const uint IOCTL_STORAGE_QUERY_PROPERTY = 0x002D1400;
    public const uint IOCTL_STORAGE_GET_DEVICE_NUMBER = 0x002D1080;
    public const uint IOCTL_STORAGE_PREDICT_FAILURE = 0x002D1100;

    public const uint IOCTL_ATA_PASS_THROUGH = 0x0004D02C;

    public const ushort ATA_FLAGS_DRDY_REQUIRED = 0x01;
    public const ushort ATA_FLAGS_DATA_IN = 0x02;

    public const byte AtaCommandSmart = 0xB0;
    public const byte AtaCommandIdentify = 0xEC;
    public const byte SmartFeatureReadData = 0xD0;
    public const byte SmartFeatureReadThresholds = 0xD1;
    public const byte SmartLbaMid = 0x4F;
    public const byte SmartLbaHigh = 0xC2;

    [StructLayout(LayoutKind.Sequential)]
    public unsafe struct AtaPassThroughEx
    {
        public ushort Length;
        public ushort AtaFlags;
        public byte PathId;
        public byte TargetId;
        public byte Lun;
        public byte ReservedAsUchar;
        public uint DataTransferLength;
        public uint TimeOutValue;
        public uint ReservedAsUlong;
        public IntPtr DataBufferOffset;
        public fixed byte PreviousTaskFile[8];
        public fixed byte CurrentTaskFile[8];
    }

    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    public static extern bool GetVolumeInformationW(
        string lpRootPathName,
        System.Text.StringBuilder lpVolumeNameBuffer,
        uint nVolumeNameSize,
        out uint lpVolumeSerialNumber,
        out uint lpMaximumComponentLength,
        out uint lpFileSystemFlags,
        System.Text.StringBuilder lpFileSystemNameBuffer,
        uint nFileSystemNameSize);

    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    public static extern SafeFileHandle CreateFileW(
        string lpFileName,
        uint dwDesiredAccess,
        uint dwShareMode,
        IntPtr lpSecurityAttributes,
        uint dwCreationDisposition,
        uint dwFlagsAndAttributes,
        IntPtr hTemplateFile);

    [DllImport("kernel32.dll", SetLastError = true)]
    public static extern bool DeviceIoControl(
        SafeFileHandle hDevice,
        uint dwIoControlCode,
        IntPtr lpInBuffer,
        uint nInBufferSize,
        IntPtr lpOutBuffer,
        uint nOutBufferSize,
        out uint lpBytesReturned,
        IntPtr lpOverlapped);

    [DllImport("kernel32.dll", SetLastError = true)]
    public static extern unsafe bool ReadFile(
        SafeFileHandle hFile,
        byte* lpBuffer,
        uint nNumberOfBytesToRead,
        out uint lpNumberOfBytesRead,
        IntPtr lpOverlapped);

    [DllImport("kernel32.dll", SetLastError = true)]
    public static extern unsafe bool WriteFile(
        SafeFileHandle hFile,
        byte* lpBuffer,
        uint nNumberOfBytesToWrite,
        out uint lpNumberOfBytesWritten,
        IntPtr lpOverlapped);

    [DllImport("kernel32.dll", SetLastError = true)]
    public static extern bool SetFilePointerEx(
        SafeFileHandle hFile,
        long liDistanceToMove,
        out long lpNewFilePointer,
        uint dwMoveMethod);
}
