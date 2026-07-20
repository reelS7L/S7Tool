using System.IO;
using System.Linq;

namespace S7Tool.DiskManagerPE;

public static class CrashLogger
{
    public static string Log(Exception? ex, string source)
    {
        string entry = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {source}\n{ex}\n\n";
        string firstWrittenPath = "";

        var targets = new List<string>();
        try
        {
            targets.AddRange(DriveInfo.GetDrives()
                .Where(d => d.DriveType == DriveType.Fixed && d.IsReady)
                .Select(d => Path.Combine(d.RootDirectory.FullName, "S7Tool", "pe-crash.log")));
        }
        catch { }

        targets.Add(@"X:\S7Tool\pe-crash.log");

        foreach (var path in targets)
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                File.AppendAllText(path, entry);
                if (firstWrittenPath == "") firstWrittenPath = path;
            }
            catch
            {
            }
        }

        return firstWrittenPath == "" ? "(impossible d'écrire le journal sur un disque)" : firstWrittenPath;
    }
}
