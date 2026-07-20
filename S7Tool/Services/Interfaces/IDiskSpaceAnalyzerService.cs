using S7Tool.Models;

namespace S7Tool.Services.Interfaces;

public record ScanProgress(long FilesScanned, long FoldersScanned, long BytesScanned, string CurrentPath);

public interface IDiskSpaceAnalyzerService
{
    Task<FileSystemNode> ScanAsync(string rootPath, Action<FileSystemNode> onRootReady, IProgress<ScanProgress> progress, CancellationToken cancellationToken);
}
