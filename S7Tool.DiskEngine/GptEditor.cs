using System.IO.Hashing;

namespace S7Tool.DiskEngine;

public sealed class GptEditor
{
    private const ulong ProtectiveMbrLba = 0;
    private const ulong PrimaryHeaderLba = 1;

    private readonly PhysicalDiskAccessor _disk;
    private readonly int _sectorSize;

    public GptEditor(PhysicalDiskAccessor disk)
    {
        _disk = disk;
        _sectorSize = disk.SectorSize > 0 ? disk.SectorSize : PhysicalDiskAccessor.DefaultSectorSize;
    }

    public GptHeader ReadPrimaryHeader() => ReadHeaderAt(PrimaryHeaderLba);

    public GptHeader ReadBackupHeader(GptHeader primary) => ReadHeaderAt(primary.BackupLba);

    private GptHeader ReadHeaderAt(ulong lba)
    {
        using var buf = new AlignedBuffer(_sectorSize, 4096);
        _disk.ReadAt((long)(lba * (ulong)_sectorSize), buf.Span);
        return GptHeader.Parse(buf.Span);
    }

    public int FindEntryIndex(Guid uniquePartitionGuid)
    {
        var entries = ReadEntries(ReadPrimaryHeader());
        for (int i = 0; i < entries.Count; i++)
        {
            if (entries[i].UniqueGuid == uniquePartitionGuid) return i;
        }
        throw new InvalidOperationException($"Aucune partition avec le GUID {uniquePartitionGuid} trouvée dans la table GPT.");
    }

    public List<GptPartitionEntry> ReadEntries(GptHeader header)
    {
        int totalBytes = checked((int)(header.NumberOfPartitionEntries * header.SizeOfPartitionEntry));
        int alignedBytes = AlignUp(totalBytes, 4096);
        using var buf = new AlignedBuffer(alignedBytes, 4096);
        _disk.ReadAt((long)(header.PartitionEntryLba * (ulong)_sectorSize), buf.Span);

        var list = new List<GptPartitionEntry>((int)header.NumberOfPartitionEntries);
        for (int i = 0; i < header.NumberOfPartitionEntries; i++)
        {
            int offset = i * (int)header.SizeOfPartitionEntry;
            list.Add(GptPartitionEntry.Parse(buf.Span.Slice(offset, (int)header.SizeOfPartitionEntry)));
        }
        return list;
    }

    public string BackupToFile(string path)
    {
        var primaryHeader = ReadPrimaryHeader();
        var backupHeader = ReadBackupHeader(primaryHeader);

        using var fs = new FileStream(path, FileMode.Create, FileAccess.Write);
        using var writer = new BinaryWriter(fs);

        void DumpSectorRange(ulong startLba, int byteCount)
        {
            int aligned = AlignUp(byteCount, 4096);
            using var buf = new AlignedBuffer(aligned, 4096);
            _disk.ReadAt((long)(startLba * (ulong)_sectorSize), buf.Span);
            writer.Write(startLba);
            writer.Write(byteCount);
            writer.Write(buf.Span[..byteCount]);
        }

        DumpSectorRange(PrimaryHeaderLba, _sectorSize);
        DumpSectorRange(primaryHeader.PartitionEntryLba, checked((int)(primaryHeader.NumberOfPartitionEntries * primaryHeader.SizeOfPartitionEntry)));
        DumpSectorRange(backupHeader.PartitionEntryLba, checked((int)(backupHeader.NumberOfPartitionEntries * backupHeader.SizeOfPartitionEntry)));
        DumpSectorRange(primaryHeader.BackupLba, _sectorSize);

        return path;
    }

    public void RestoreFromFile(string path)
    {
        using var fs = new FileStream(path, FileMode.Open, FileAccess.Read);
        using var reader = new BinaryReader(fs);

        while (fs.Position < fs.Length)
        {
            ulong lba = reader.ReadUInt64();
            int byteCount = reader.ReadInt32();
            byte[] raw = reader.ReadBytes(byteCount);

            int aligned = AlignUp(byteCount, 4096);
            using var buf = new AlignedBuffer(aligned, 4096);
            raw.CopyTo(buf.Span);
            _disk.WriteAt((long)(lba * (ulong)_sectorSize), buf.Span[..aligned]);
        }
    }

    public void UpdatePartitionOffset(int entryIndex, ulong newFirstLba, ulong newLastLba)
    {
        var primaryHeader = ReadPrimaryHeader();
        var backupHeader = ReadBackupHeader(primaryHeader);

        var primaryEntries = ReadEntries(primaryHeader);
        var backupEntries = ReadEntries(backupHeader);

        if (entryIndex < 0 || entryIndex >= primaryEntries.Count)
            throw new ArgumentOutOfRangeException(nameof(entryIndex));

        var updated = primaryEntries[entryIndex];
        updated.FirstLba = newFirstLba;
        updated.LastLba = newLastLba;
        primaryEntries[entryIndex] = updated;
        backupEntries[entryIndex] = updated;

        WriteHeaderAndEntries(backupHeader, backupEntries);
        WriteHeaderAndEntries(primaryHeader, primaryEntries);
    }

    private void WriteHeaderAndEntries(GptHeader header, List<GptPartitionEntry> entries, bool freshHeaderSector = false)
    {
        int totalBytes = checked((int)(header.NumberOfPartitionEntries * header.SizeOfPartitionEntry));
        int alignedBytes = AlignUp(totalBytes, 4096);
        using var entriesBuf = new AlignedBuffer(alignedBytes, 4096);

        for (int i = 0; i < entries.Count; i++)
        {
            int offset = i * (int)header.SizeOfPartitionEntry;
            entries[i].WriteTo(entriesBuf.Span.Slice(offset, (int)header.SizeOfPartitionEntry));
        }

        header.PartitionEntryArrayCrc32 = Crc32.HashToUInt32(entriesBuf.Span[..totalBytes]);

        _disk.WriteAt((long)(header.PartitionEntryLba * (ulong)_sectorSize), entriesBuf.Span[..alignedBytes]);

        using var headerBuf = new AlignedBuffer(_sectorSize, 4096);
        if (freshHeaderSector)
            headerBuf.Span.Clear();
        else
            _disk.ReadAt((long)(header.CurrentLba * (ulong)_sectorSize), headerBuf.Span);
        header.WriteTo(headerBuf.Span);
        _disk.WriteAt((long)(header.CurrentLba * (ulong)_sectorSize), headerBuf.Span);
    }

    public bool EnsureBackupAtDiskEnd(long diskSizeBytes)
    {
        var primary = ReadPrimaryHeader();

        ulong lastLba = (ulong)(diskSizeBytes / _sectorSize) - 1;
        int entriesBytes = checked((int)(primary.NumberOfPartitionEntries * primary.SizeOfPartitionEntry));
        ulong entrySectors = (ulong)(AlignUp(entriesBytes, _sectorSize) / _sectorSize);
        ulong wantEntriesLba = lastLba - entrySectors;
        ulong wantLastUsable = wantEntriesLba - 1;

        if (primary.BackupLba == lastLba && primary.LastUsableLba == wantLastUsable)
            return false;

        var entries = ReadEntries(primary);

        var backup = new GptHeader
        {
            Revision = primary.Revision,
            HeaderSize = primary.HeaderSize,
            CurrentLba = lastLba,
            BackupLba = PrimaryHeaderLba,
            FirstUsableLba = primary.FirstUsableLba,
            LastUsableLba = wantLastUsable,
            DiskGuid = primary.DiskGuid,
            PartitionEntryLba = wantEntriesLba,
            NumberOfPartitionEntries = primary.NumberOfPartitionEntries,
            SizeOfPartitionEntry = primary.SizeOfPartitionEntry,
        };

        WriteHeaderAndEntries(backup, entries, freshHeaderSector: true);

        primary.BackupLba = lastLba;
        primary.LastUsableLba = wantLastUsable;
        WriteHeaderAndEntries(primary, entries);

        return true;
    }

    private static int AlignUp(int value, int alignment) => (value + alignment - 1) / alignment * alignment;
}
