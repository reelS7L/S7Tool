namespace S7Tool.Models;

public class FileSystemNode
{
    private long _sizeBytes;
    private readonly object _childrenLock = new();

    public required string Name { get; init; }
    public required string FullPath { get; init; }
    public required bool IsDirectory { get; init; }
    public DateTime LastWriteTimeUtc { get; init; }
    public string? Extension { get; init; }
    public FileSystemNode? Parent { get; set; }
    public List<FileSystemNode> Children { get; } = new();

    public long SizeBytes
    {
        get => Interlocked.Read(ref _sizeBytes);
        set => Interlocked.Exchange(ref _sizeBytes, value);
    }

    public int FileCount { get; set; }
    public int FolderCount { get; set; }

    public double SizeGb => Math.Round(SizeBytes / 1024.0 / 1024.0 / 1024.0, 2);
    public double SizeMb => Math.Round(SizeBytes / 1024.0 / 1024.0, 1);

    public string SizeDisplay => SizeBytes switch
    {
        >= 1024L * 1024 * 1024 => $"{SizeGb:0.##} Go",
        >= 1024L * 1024 => $"{SizeMb:0.#} Mo",
        >= 1024 => $"{Math.Round(SizeBytes / 1024.0, 1)} Ko",
        _ => $"{SizeBytes} o"
    };

    public double PercentOfParent { get; set; }

    public void AddChild(FileSystemNode child)
    {
        lock (_childrenLock) Children.Add(child);
    }

    public List<FileSystemNode> GetChildrenSnapshot()
    {
        lock (_childrenLock) return new List<FileSystemNode>(Children);
    }

    public void BubbleSize(long delta)
    {
        for (var n = this; n is not null; n = n.Parent)
            Interlocked.Add(ref n._sizeBytes, delta);
    }
}
