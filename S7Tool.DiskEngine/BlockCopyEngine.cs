using System.Diagnostics;

namespace S7Tool.DiskEngine;

public static class BlockCopyEngine
{
    public const int DefaultBlockSize = 4 * 1024 * 1024;
    private const int MaxReadRetries = 3;
    private const int SectorAlignment = 4096;

    public static async Task RunDiskToDiskAsync(
        int sourceDiskNumber,
        int destinationDiskNumber,
        bool verify,
        string? journalPath,
        Action<CloneProgress> onProgress,
        Action<string> onLog,
        CancellationToken ct)
    {
        using var source = PhysicalDiskAccessor.OpenForRead(sourceDiskNumber);
        using var destination = PhysicalDiskAccessor.OpenForWrite(destinationDiskNumber);

        if (destination.DiskSizeBytes < source.DiskSizeBytes)
            throw new InvalidOperationException("Le disque de destination est plus petit que le disque source.");

        string key = $"disk{sourceDiskNumber}_to_disk{destinationDiskNumber}";
        await RunAsync(source, destination, source.DiskSizeBytes, verify,
            string.IsNullOrEmpty(journalPath) ? CloneJournal.PathFor(key) : journalPath,
            $@"\\.\PhysicalDrive{sourceDiskNumber}", $@"\\.\PhysicalDrive{destinationDiskNumber}",
            onProgress, onLog, ct);
    }

    public static Task RunIntraDiskMoveAsync(
        int diskNumber,
        long sourceOffset,
        long destinationOffset,
        long lengthBytes,
        string? journalPath,
        Action<CloneProgress> onProgress,
        Action<string> onLog,
        CancellationToken ct)
    {
        return Task.Run(() =>
        {
            using var disk = PhysicalDiskAccessor.OpenForWrite(diskNumber);

            int blockSize = DefaultBlockSize;
            using var buffer = new AlignedBuffer(blockSize, SectorAlignment);
            using var verifyBuffer = new AlignedBuffer(blockSize, SectorAlignment);
            bool forward = destinationOffset <= sourceOffset;

            string key = $"disk{diskNumber}_move_{sourceOffset}_to_{destinationOffset}";
            string path = string.IsNullOrEmpty(journalPath) ? CloneJournal.PathFor(key) : journalPath;

            var journal = CloneJournal.TryLoad(path);
            long done = 0;
            if (journal is not null && journal.TotalBytes == lengthBytes && journal.CompletedUtc is null)
            {
                done = journal.LastConfirmedOffset;
                onLog($"Reprise du déplacement après interruption : {done:N0} sur {lengthBytes:N0} octets déjà déplacés.");
            }
            else
            {
                journal = new CloneJournal
                {
                    SourcePath = $@"\\.\PhysicalDrive{diskNumber} @ {sourceOffset:N0}",
                    DestinationPath = $@"\\.\PhysicalDrive{diskNumber} @ {destinationOffset:N0}",
                    TotalBytes = lengthBytes,
                    BlockSize = blockSize,
                    StartedUtc = DateTime.UtcNow
                };
            }

            var sw = Stopwatch.StartNew();
            long reported = 0;

            while (done < lengthBytes)
            {
                ct.ThrowIfCancellationRequested();
                int currentLen = (int)Math.Min(blockSize, lengthBytes - done);

                long srcOff = forward ? sourceOffset + done : sourceOffset + (lengthBytes - done - currentLen);
                long dstOff = forward ? destinationOffset + done : destinationOffset + (lengthBytes - done - currentLen);

                var span = buffer.Span.Slice(0, currentLen);
                disk.ReadAt(srcOff, span);
                disk.WriteAt(dstOff, span);

                var verifySpan = verifyBuffer.Span.Slice(0, currentLen);
                disk.ReadAt(dstOff, verifySpan);
                if (!span.SequenceEqual(verifySpan))
                    throw new IOException($"Échec de vérification pendant le déplacement à l'offset destination {dstOff:N0} : les données écrites ne correspondent pas.");

                done += currentLen;
                reported += currentLen;
                journal.LastConfirmedOffset = done;

                if (sw.ElapsedMilliseconds >= 500)
                {
                    double mbps = reported / 1024.0 / 1024.0 / sw.Elapsed.TotalSeconds;
                    var eta = mbps > 0.01 ? TimeSpan.FromSeconds((lengthBytes - done) / 1024.0 / 1024.0 / mbps) : TimeSpan.Zero;
                    onProgress(new CloneProgress(done, lengthBytes, mbps, eta, "Déplacement des blocs"));
                    journal.Save(path);
                    reported = 0;
                    sw.Restart();
                }
            }

            journal.CompletedUtc = DateTime.UtcNow;
            journal.Save(path);
            onLog("Déplacement des blocs terminé et vérifié.");
        }, ct);
    }

    private static async Task RunAsync(
        PhysicalDiskAccessor source,
        PhysicalDiskAccessor destination,
        long totalBytes,
        bool verify,
        string journalPath,
        string sourcePathLabel,
        string destinationPathLabel,
        Action<CloneProgress> onProgress,
        Action<string> onLog,
        CancellationToken ct)
    {
        int blockSize = DefaultBlockSize;

        var journal = CloneJournal.TryLoad(journalPath);
        long startOffset = 0;
        if (journal is not null && journal.TotalBytes == totalBytes && journal.CompletedUtc is null)
        {
            startOffset = journal.LastConfirmedOffset;
            onLog($"Reprise après interruption détectée : reprise à l'offset {startOffset:N0} sur {totalBytes:N0}.");
        }
        else
        {
            journal = new CloneJournal
            {
                SourcePath = sourcePathLabel,
                DestinationPath = destinationPathLabel,
                TotalBytes = totalBytes,
                BlockSize = blockSize,
                VerifyEnabled = verify,
                StartedUtc = DateTime.UtcNow
            };
        }

        using var readBuffer = new AlignedBuffer(blockSize, SectorAlignment);
        using var verifyBuffer = verify ? new AlignedBuffer(blockSize, SectorAlignment) : null;

        var sw = Stopwatch.StartNew();
        long bytesSinceReport = 0;
        long offset = startOffset;

        while (offset < totalBytes)
        {
            ct.ThrowIfCancellationRequested();

            int currentLen = (int)Math.Min(blockSize, totalBytes - offset);
            var readSpan = readBuffer.Span.Slice(0, currentLen);

            if (!TryReadWithRetry(source, offset, readSpan, onLog))
            {
                journal.SkippedBadBlocks.Add($"{offset}:{currentLen}");
                onLog($"AVERTISSEMENT : bloc illisible ignoré à l'offset {offset:N0} (taille {currentLen} octets), rempli de zéros.");
                readSpan.Clear();
            }

            destination.WriteAt(offset, readSpan);

            if (verify)
            {
                var verifySpan = verifyBuffer!.Span.Slice(0, currentLen);
                destination.ReadAt(offset, verifySpan);
                if (!readSpan.SequenceEqual(verifySpan))
                    throw new IOException($"Échec de vérification à l'offset {offset:N0} : la destination ne correspond pas à la source après écriture.");
            }

            offset += currentLen;
            bytesSinceReport += currentLen;
            journal.LastConfirmedOffset = offset;

            if (sw.ElapsedMilliseconds >= 500)
            {
                double mbps = bytesSinceReport / 1024.0 / 1024.0 / sw.Elapsed.TotalSeconds;
                double remainingMb = (totalBytes - offset) / 1024.0 / 1024.0;
                var eta = mbps > 0.01 ? TimeSpan.FromSeconds(remainingMb / mbps) : TimeSpan.Zero;
                onProgress(new CloneProgress(offset, totalBytes, mbps, eta, verify ? "Copie + vérification" : "Copie secteur par secteur"));
                journal.Save(journalPath);
                bytesSinceReport = 0;
                sw.Restart();
            }

            await Task.Yield();
        }

        journal.CompletedUtc = DateTime.UtcNow;
        journal.Save(journalPath);
        onProgress(new CloneProgress(totalBytes, totalBytes, 0, TimeSpan.Zero, "Terminé"));

        onLog(journal.SkippedBadBlocks.Count > 0
            ? $"Clonage terminé avec {journal.SkippedBadBlocks.Count} bloc(s) illisible(s) ignoré(s) — voir le journal {journalPath}."
            : "Clonage terminé sans erreur" + (verify ? ", intégralement vérifié bloc par bloc." : "."));
    }

    private static bool TryReadWithRetry(PhysicalDiskAccessor source, long offset, Span<byte> buffer, Action<string> onLog)
    {
        for (int attempt = 1; attempt <= MaxReadRetries; attempt++)
        {
            try
            {
                source.ReadAt(offset, buffer);
                return true;
            }
            catch (IOException) when (attempt < MaxReadRetries)
            {
                onLog($"Secteur illisible à l'offset {offset:N0}, nouvelle tentative {attempt}/{MaxReadRetries}...");
            }
            catch (IOException)
            {
                return false;
            }
        }
        return false;
    }
}
