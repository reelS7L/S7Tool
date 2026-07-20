using System.Runtime.InteropServices;

namespace S7Tool.Helpers;

public static class NativeShell
{
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct SHELLEXECUTEINFO
    {
        public int cbSize;
        public uint fMask;
        public IntPtr hwnd;
        public string? lpVerb;
        public string lpFile;
        public string? lpParameters;
        public string? lpDirectory;
        public int nShow;
        public IntPtr hInstApp;
        public IntPtr lpIDList;
        public string? lpClass;
        public IntPtr hkeyClass;
        public uint dwHotKey;
        public IntPtr hIcon;
        public IntPtr hProcess;
    }

    private const uint SEE_MASK_INVOKEIDLIST = 0x0000000C;

    [DllImport("shell32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool ShellExecuteEx(ref SHELLEXECUTEINFO lpExecInfo);

    public static void ShowFileProperties(string path)
    {
        var info = new SHELLEXECUTEINFO
        {
            lpVerb = "properties",
            lpFile = path,
            nShow = 1,
            fMask = SEE_MASK_INVOKEIDLIST
        };
        info.cbSize = Marshal.SizeOf(info);
        ShellExecuteEx(ref info);
    }
}
