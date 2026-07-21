using S7Tool.Models;
using S7Tool.Services.Interfaces;
using System.IO;

namespace S7Tool.Services;

public class DiskSpaceAnalyzerService : IDiskSpaceAnalyzerService
{
    private const int MaxConcurrency = 64;

    private long _filesScanned;
    private long _foldersScanned;
    private long _bytesScanned;
    private readonly object _progressLock = new();
    private DateTime _lastProgressReport;

    public async Task<FileSystemNode> ScanAsync(string rootPath, Action<FileSystemNode> onRootReady, IProgress<ScanProgress> progress, CancellationToken cancellationToken)
    {
        _filesScanned = 0;
        _foldersScanned = 0;
        _bytesScanned = 0;
        _lastProgressReport = DateTime.MinValue;

        var root = new FileSystemNode
        {
            Name = string.IsNullOrEmpty(Path.GetFileName(rootPath)) ? rootPath : Path.GetFileName(rootPath),
            FullPath = rootPath,
            IsDirectory = true,
            Parent = null
        };
        onRootReady(root);

        using var semaphore = new SemaphoreSlim(MaxConcurrency);
        await Task.Run(() => ScanIntoAsync(root, semaphore, progress, cancellationToken), cancellationToken);

        progress.Report(new ScanProgress(Interlocked.Read(ref _filesScanned), Interlocked.Read(ref _foldersScanned), Interlocked.Read(ref _bytesScanned), rootPath));
        return root;
    }

    private async Task ScanIntoAsync(FileSystemNode node, SemaphoreSlim semaphore, IProgress<ScanProgress> progress, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();

        List<FileSystemInfo> entries;
        try
        {
            entries = new DirectoryInfo(node.FullPath).EnumerateFileSystemInfos().ToList();
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or IOException)
        {
            Interlocked.Increment(ref _foldersScanned);
            ReportProgressThrottled(progress, node.FullPath);
            return;
        }

        var subDirTasks = new List<Task>();

        foreach (var entry in entries)
        {
            ct.ThrowIfCancellationRequested();

            FileAttributes attributes;
            try
            {
                attributes = entry.Attributes;
            }
            catch (Exception ex) when (ex is UnauthorizedAccessException or IOException)
            {
                continue;
            }

            if (attributes.HasFlag(FileAttributes.ReparsePoint))
                continue;

            if (entry is DirectoryInfo)
            {
                var childNode = new FileSystemNode
                {
                    Name = entry.Name,
                    FullPath = entry.FullName,
                    IsDirectory = true,
                    Parent = node
                };
                node.AddChild(childNode);
                subDirTasks.Add(ScanDirectoryThrottledAsync(childNode, semaphore, progress, ct));
            }
            else if (entry is FileInfo info)
            {
                try
                {
                    var fileNode = new FileSystemNode
                    {
                        Name = info.Name,
                        FullPath = info.FullName,
                        IsDirectory = false,
                        SizeBytes = info.Length,
                        LastWriteTimeUtc = info.LastWriteTimeUtc,
                        Extension = string.IsNullOrEmpty(info.Extension) ? null : info.Extension.TrimStart('.').ToLowerInvariant(),
                        Parent = node
                    };
                    node.AddChild(fileNode);
                    node.BubbleSize(info.Length);

                    Interlocked.Increment(ref _filesScanned);
                    Interlocked.Add(ref _bytesScanned, info.Length);
                }
                catch (Exception ex) when (ex is UnauthorizedAccessException or IOException)
                {
                }
            }
        }

        await Task.WhenAll(subDirTasks);

        var childrenSnapshot = node.GetChildrenSnapshot();
        node.FileCount = childrenSnapshot.Count(c => !c.IsDirectory) + childrenSnapshot.Where(c => c.IsDirectory).Sum(c => c.FileCount);
        node.FolderCount = childrenSnapshot.Count(c => c.IsDirectory) + childrenSnapshot.Where(c => c.IsDirectory).Sum(c => c.FolderCount);

        Interlocked.Increment(ref _foldersScanned);
        ReportProgressThrottled(progress, node.FullPath);
    }

    private async Task ScanDirectoryThrottledAsync(FileSystemNode node, SemaphoreSlim semaphore, IProgress<ScanProgress> progress, CancellationToken ct)
    {
        await semaphore.WaitAsync(ct);
        try
        {
            await ScanIntoAsync(node, semaphore, progress, ct);
        }
        finally
        {
            semaphore.Release();
        }
    }

    private void ReportProgressThrottled(IProgress<ScanProgress> progress, string currentPath)
    {
        lock (_progressLock)
        {
            var now = DateTime.UtcNow;
            if ((now - _lastProgressReport).TotalMilliseconds < 200) return;
            _lastProgressReport = now;
        }

        progress.Report(new ScanProgress(
            Interlocked.Read(ref _filesScanned),
            Interlocked.Read(ref _foldersScanned),
            Interlocked.Read(ref _bytesScanned),
            currentPath));
    }
}
