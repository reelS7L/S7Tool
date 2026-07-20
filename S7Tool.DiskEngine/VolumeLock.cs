using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace S7Tool.DiskEngine;

public sealed class VolumeLock : IDisposable
{
    private readonly SafeFileHandle _handle;
    public string DriveLetter { get; }

    private VolumeLock(SafeFileHandle handle, string driveLetter)
    {
        _handle = handle;
        DriveLetter = driveLetter;
    }

    public static VolumeLock LockAndDismount(string driveLetter)
    {
        string letter = driveLetter.TrimEnd(':', '\\');
        string path = $@"\\.\{letter}:";

        var handle = NativeMethods.CreateFileW(
            path, NativeMethods.GENERIC_READ | NativeMethods.GENERIC_WRITE,
            NativeMethods.FILE_SHARE_READ | NativeMethods.FILE_SHARE_WRITE,
            IntPtr.Zero, NativeMethods.OPEN_EXISTING, 0, IntPtr.Zero);

        if (handle.IsInvalid)
        {
            int err = Marshal.GetLastWin32Error();
            handle.Dispose();
            throw new IOException($"Impossible d'ouvrir le volume {path} (erreur Windows {err}).");
        }

        if (!NativeMethods.DeviceIoControl(handle, NativeMethods.FSCTL_LOCK_VOLUME, IntPtr.Zero, 0, IntPtr.Zero, 0, out _, IntPtr.Zero))
        {
            int err = Marshal.GetLastWin32Error();
            handle.Dispose();
            throw new IOException(
                $"Impossible de verrouiller le volume {path} (erreur Windows {err}) — ferme tous les " +
                "programmes/fenêtres qui l'utilisent (y compris l'Explorateur Windows) et réessaie.");
        }

        NativeMethods.DeviceIoControl(handle, NativeMethods.FSCTL_DISMOUNT_VOLUME, IntPtr.Zero, 0, IntPtr.Zero, 0, out _, IntPtr.Zero);

        return new VolumeLock(handle, letter);
    }

    public void Dispose()
    {
        NativeMethods.DeviceIoControl(_handle, NativeMethods.FSCTL_UNLOCK_VOLUME, IntPtr.Zero, 0, IntPtr.Zero, 0, out _, IntPtr.Zero);
        _handle.Dispose();
    }
}
