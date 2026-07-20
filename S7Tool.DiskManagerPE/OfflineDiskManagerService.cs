using System.IO;
using System.Text.RegularExpressions;
using S7Tool.DiskEngine;
using S7Tool.DiskEngine.Models;

namespace S7Tool.DiskManagerPE;

public class OfflineDiskManagerService
{
    private static readonly HashSet<string> SupportedFileSystems = new(StringComparer.OrdinalIgnoreCase) { "NTFS", "FAT32", "exFAT" };

    public Task<List<DiskInfo>> GetDisksAsync() => Task.Run(DiskEnumerator.GetDisks);

    public Task<List<DiskHealthInfo>> GetHealthAsync(IEnumerable<int> diskNumbers) => Task.Run(() => DiskEnumerator.GetHealth(diskNumbers));

    private static void ValidateFileSystem(string fileSystem)
    {
        if (!SupportedFileSystems.Contains(fileSystem))
            throw new InvalidOperationException($"Système de fichiers non pris en charge : {fileSystem}. Utilise NTFS, FAT32 ou exFAT.");
    }

    private static async Task<DiskPartitionInfo> FindPartitionAsync(int diskNumber, int partitionNumber)
    {
        var disks = await Task.Run(DiskEnumerator.GetDisks);
        var disk = disks.FirstOrDefault(d => d.DiskNumber == diskNumber)
            ?? throw new InvalidOperationException("Disque introuvable.");
        var partition = disk.Partitions.FirstOrDefault(p => p.PartitionNumber == partitionNumber)
            ?? throw new InvalidOperationException("Partition introuvable.");
        return partition;
    }

    public async Task CreatePartitionAsync(int diskNumber, long? sizeBytes, string fileSystem, string? label)
    {
        ValidateFileSystem(fileSystem);
        string sizeArg = sizeBytes.HasValue ? $"size={sizeBytes.Value / 1024 / 1024}" : "";
        string labelArg = string.IsNullOrWhiteSpace(label) ? "Data" : label.Replace("\"", "").Replace("=", "");

        string script = $@"select disk {diskNumber}
create partition primary {sizeArg}
format fs={fileSystem} label=""{labelArg}"" quick
assign
";
        await DiskpartRunner.RunAsync(script);
    }

    public async Task DeletePartitionAsync(int diskNumber, int partitionNumber)
    {
        string script = $@"select disk {diskNumber}
select partition {partitionNumber}
delete partition
";
        await DiskpartRunner.RunAsync(script);
    }

    public async Task FormatPartitionAsync(int diskNumber, int partitionNumber, string fileSystem, string? label, bool quickFormat = true)
    {
        ValidateFileSystem(fileSystem);
        string labelArg = string.IsNullOrWhiteSpace(label) ? "" : $@" label=""{label.Replace("\"", "").Replace("=", "")}""";
        string script = $@"select disk {diskNumber}
select partition {partitionNumber}
format fs={fileSystem}{labelArg}{(quickFormat ? " quick" : "")}
";
        await DiskpartRunner.RunAsync(script);
    }

    public async Task RenamePartitionAsync(int diskNumber, int partitionNumber, string newLabel)
    {
        var partition = await FindPartitionAsync(diskNumber, partitionNumber);
        if (string.IsNullOrEmpty(partition.DriveLetter))
            throw new InvalidOperationException("Cette partition n'a pas de lettre de lecteur — assigne-en une avant de la renommer.");

        await DiskpartRunner.RunLabelAsync(partition.DriveLetter, newLabel);
    }

    public async Task SetPartitionTypeAsync(int diskNumber, int partitionNumber, string gptTypeGuid)
    {
        string guid = gptTypeGuid.Trim('{', '}');
        string script = $@"select disk {diskNumber}
select partition {partitionNumber}
set id={{{guid}}}
";
        await DiskpartRunner.RunAsync(script);
    }

    public async Task SetPartitionHiddenAsync(int diskNumber, int partitionNumber, bool hidden)
    {
        string attributes = hidden ? "0xC000000000000000" : "0x0000000000000000";
        string script = $@"select disk {diskNumber}
select partition {partitionNumber}
gpt attributes={attributes}
";
        await DiskpartRunner.RunAsync(script);
    }

    public async Task<(long MinBytes, long MaxBytes)> GetSupportedSizeRangeAsync(int diskNumber, int partitionNumber)
    {
        var disks = await Task.Run(DiskEnumerator.GetDisks);
        var disk = disks.FirstOrDefault(d => d.DiskNumber == diskNumber)
            ?? throw new InvalidOperationException("Disque introuvable.");

        var ordered = disk.Partitions.OrderBy(p => p.StartOffsetBytes).ToList();
        int index = ordered.FindIndex(p => !p.IsUnallocated && p.PartitionNumber == partitionNumber);
        if (index < 0) throw new InvalidOperationException("Partition introuvable.");
        var partition = ordered[index];

        long extendableBytes = index + 1 < ordered.Count && ordered[index + 1].IsUnallocated
            ? ordered[index + 1].SizeBytes
            : 0;

        long maxShrinkBytes = 0;
        try
        {
            string output = await DiskpartRunner.RunAsync($"select disk {diskNumber}\r\nselect partition {partitionNumber}\r\nshrink querymax\r\n");
            maxShrinkBytes = ParseFirstSizeToBytes(output);
        }
        catch
        {
        }

        return (Math.Max(1024L * 1024, partition.SizeBytes - maxShrinkBytes), partition.SizeBytes + extendableBytes);
    }

    private static long ParseFirstSizeToBytes(string diskpartOutput)
    {
        var match = Regex.Match(diskpartOutput, @"(\d+(?:[.,]\d+)?)\s*(KB|MB|GB|TB|Ko|Mo|Go|To)\b", RegexOptions.IgnoreCase);
        if (!match.Success) return 0;

        double value = double.Parse(match.Groups[1].Value.Replace(',', '.'), System.Globalization.CultureInfo.InvariantCulture);
        double multiplier = char.ToUpperInvariant(match.Groups[2].Value[0]) switch
        {
            'K' => 1024d,
            'M' => 1024d * 1024,
            'G' => 1024d * 1024 * 1024,
            'T' => 1024d * 1024 * 1024 * 1024,
            _ => 1d
        };
        return (long)(value * multiplier);
    }

    public async Task ResizePartitionAsync(int diskNumber, int partitionNumber, long newSizeBytes)
    {
        await Task.Run(() => EnsureGptConsistency(diskNumber));

        var partition = await FindPartitionAsync(diskNumber, partitionNumber);

        var (minBytes, maxBytes) = await GetSupportedSizeRangeAsync(diskNumber, partitionNumber);
        newSizeBytes = Math.Clamp(newSizeBytes, minBytes, maxBytes);

        long deltaMb = (newSizeBytes - partition.SizeBytes) / (1024 * 1024);
        if (deltaMb == 0) return;

        string command = deltaMb < 0
            ? $"shrink desired={-deltaMb}"
            : $"extend size={deltaMb}";

        string output = await DiskpartRunner.RunAsync($"select disk {diskNumber}\r\nselect partition {partitionNumber}\r\n{command}\r\n");

        var updated = await FindPartitionAsync(diskNumber, partitionNumber);
        const long tolerance = 8L * 1024 * 1024;
        if (Math.Abs(updated.SizeBytes - newSizeBytes) > tolerance)
            throw new InvalidOperationException(
                $"diskpart n'a pas appliqué le redimensionnement (taille toujours à {updated.SizeGb} Go).\n\nSortie diskpart :\n{output}");
    }


    public async Task SectorCloneDiskAsync(
        int sourceDiskNumber,
        int destinationDiskNumber,
        bool verify,
        Action<string> onLog,
        Action<CloneProgress> onProgress,
        CancellationToken cancellationToken)
    {
        var disks = await Task.Run(DiskEnumerator.GetDisks);
        var sourceDisk = disks.FirstOrDefault(d => d.DiskNumber == sourceDiskNumber) ?? throw new InvalidOperationException("Disque source introuvable.");
        var destDisk = disks.FirstOrDefault(d => d.DiskNumber == destinationDiskNumber) ?? throw new InvalidOperationException("Disque de destination introuvable.");

        if (destDisk.SizeBytes < sourceDisk.SizeBytes)
            throw new InvalidOperationException($"Le disque de destination ({destDisk.SizeGb} Go) est plus petit que le disque source ({sourceDisk.SizeGb} Go).");

        var locks = new List<VolumeLock>();
        try
        {
            foreach (var p in sourceDisk.Partitions.Where(p => !string.IsNullOrEmpty(p.DriveLetter)))
            {
                onLog($"Verrouillage du volume source {p.DriveLetter}:...");
                locks.Add(VolumeLock.LockAndDismount(p.DriveLetter!));
            }
            foreach (var p in destDisk.Partitions.Where(p => !string.IsNullOrEmpty(p.DriveLetter)))
            {
                onLog($"Verrouillage du volume destination {p.DriveLetter}:...");
                locks.Add(VolumeLock.LockAndDismount(p.DriveLetter!));
            }

            await BlockCopyEngine.RunDiskToDiskAsync(sourceDiskNumber, destinationDiskNumber, verify, null, onProgress, onLog, cancellationToken);

            if (destDisk.SizeBytes > sourceDisk.SizeBytes)
            {
                try
                {
                    using var destAccessor = PhysicalDiskAccessor.OpenForWrite(destinationDiskNumber);
                    if (new GptEditor(destAccessor).EnsureBackupAtDiskEnd(destAccessor.DiskSizeBytes))
                        onLog("Table GPT ajustée à la taille du nouveau disque (secours repositionné en fin de disque).");
                }
                catch (Exception ex)
                {
                    onLog($"AVERTISSEMENT : ajustement de la table GPT impossible ({ex.Message}) — disque non-GPT ou déjà cohérent.");
                }
            }
        }
        finally
        {
            foreach (var l in locks) l.Dispose();
        }
    }

    private static void EnsureGptConsistency(int diskNumber, Action<string>? onLog = null)
    {
        try
        {
            using var accessor = PhysicalDiskAccessor.OpenForWrite(diskNumber);
            if (new GptEditor(accessor).EnsureBackupAtDiskEnd(accessor.DiskSizeBytes))
                onLog?.Invoke("Table GPT secondaire repositionnée en fin de disque (héritée d'un clonage depuis un disque plus petit).");
        }
        catch
        {
        }
    }

}
