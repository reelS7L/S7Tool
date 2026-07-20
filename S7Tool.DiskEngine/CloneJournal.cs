using System.Text.Json;

namespace S7Tool.DiskEngine;

public sealed class CloneJournal
{
    public string SourcePath { get; set; } = "";
    public string DestinationPath { get; set; } = "";
    public long TotalBytes { get; set; }
    public long BlockSize { get; set; }
    public long LastConfirmedOffset { get; set; }
    public bool VerifyEnabled { get; set; }
    public List<string> SkippedBadBlocks { get; set; } = new();
    public DateTime StartedUtc { get; set; }
    public DateTime? CompletedUtc { get; set; }

    public static string PathFor(string journalKey) =>
        Path.Combine(Path.GetTempPath(), "S7Tool", $"clone_{journalKey}.journal.json");

    public void Save(string path)
    {
        var dir = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
        File.WriteAllText(path, JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true }));
    }

    public static CloneJournal? TryLoad(string path)
    {
        if (!File.Exists(path)) return null;
        try { return JsonSerializer.Deserialize<CloneJournal>(File.ReadAllText(path)); }
        catch { return null; }
    }
}

public readonly record struct CloneProgress(long BytesDone, long TotalBytes, double MegabytesPerSecond, TimeSpan Eta, string Phase);
