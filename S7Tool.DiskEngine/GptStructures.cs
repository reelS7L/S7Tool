using System.IO.Hashing;
using System.Text;

namespace S7Tool.DiskEngine;

public sealed class GptHeader
{
    public const int HeaderOffsetCrc32 = 16;

    public uint Revision;
    public uint HeaderSize;
    public ulong CurrentLba;
    public ulong BackupLba;
    public ulong FirstUsableLba;
    public ulong LastUsableLba;
    public Guid DiskGuid;
    public ulong PartitionEntryLba;
    public uint NumberOfPartitionEntries;
    public uint SizeOfPartitionEntry;
    public uint PartitionEntryArrayCrc32;

    public static GptHeader Parse(ReadOnlySpan<byte> sector)
    {
        if (Encoding.ASCII.GetString(sector[..8]) != "EFI PART")
            throw new InvalidOperationException("Signature GPT invalide — ce disque n'est peut-être pas partitionné en GPT.");

        return new GptHeader
        {
            Revision = BitConverter.ToUInt32(sector.Slice(8, 4)),
            HeaderSize = BitConverter.ToUInt32(sector.Slice(12, 4)),
            CurrentLba = BitConverter.ToUInt64(sector.Slice(24, 8)),
            BackupLba = BitConverter.ToUInt64(sector.Slice(32, 8)),
            FirstUsableLba = BitConverter.ToUInt64(sector.Slice(40, 8)),
            LastUsableLba = BitConverter.ToUInt64(sector.Slice(48, 8)),
            DiskGuid = new Guid(sector.Slice(56, 16)),
            PartitionEntryLba = BitConverter.ToUInt64(sector.Slice(72, 8)),
            NumberOfPartitionEntries = BitConverter.ToUInt32(sector.Slice(80, 4)),
            SizeOfPartitionEntry = BitConverter.ToUInt32(sector.Slice(84, 4)),
            PartitionEntryArrayCrc32 = BitConverter.ToUInt32(sector.Slice(88, 4)),
        };
    }

    public void WriteTo(Span<byte> sector)
    {
        Encoding.ASCII.GetBytes("EFI PART").CopyTo(sector);
        BitConverter.TryWriteBytes(sector.Slice(8, 4), Revision);
        BitConverter.TryWriteBytes(sector.Slice(12, 4), HeaderSize);
        BitConverter.TryWriteBytes(sector.Slice(16, 4), 0u);
        BitConverter.TryWriteBytes(sector.Slice(24, 8), CurrentLba);
        BitConverter.TryWriteBytes(sector.Slice(32, 8), BackupLba);
        BitConverter.TryWriteBytes(sector.Slice(40, 8), FirstUsableLba);
        BitConverter.TryWriteBytes(sector.Slice(48, 8), LastUsableLba);
        DiskGuid.ToByteArray().CopyTo(sector.Slice(56, 16));
        BitConverter.TryWriteBytes(sector.Slice(72, 8), PartitionEntryLba);
        BitConverter.TryWriteBytes(sector.Slice(80, 4), NumberOfPartitionEntries);
        BitConverter.TryWriteBytes(sector.Slice(84, 4), SizeOfPartitionEntry);
        BitConverter.TryWriteBytes(sector.Slice(88, 4), PartitionEntryArrayCrc32);

        uint crc = Crc32.HashToUInt32(sector[..(int)HeaderSize]);
        BitConverter.TryWriteBytes(sector.Slice(16, 4), crc);
    }
}

public struct GptPartitionEntry
{
    public Guid TypeGuid;
    public Guid UniqueGuid;
    public ulong FirstLba;
    public ulong LastLba;
    public ulong Attributes;
    public string Name;

    public readonly bool IsEmpty => TypeGuid == Guid.Empty;

    public static GptPartitionEntry Parse(ReadOnlySpan<byte> entry)
    {
        string name = Encoding.Unicode.GetString(entry.Slice(56, 72)).TrimEnd('\0');
        return new GptPartitionEntry
        {
            TypeGuid = new Guid(entry.Slice(0, 16)),
            UniqueGuid = new Guid(entry.Slice(16, 16)),
            FirstLba = BitConverter.ToUInt64(entry.Slice(32, 8)),
            LastLba = BitConverter.ToUInt64(entry.Slice(40, 8)),
            Attributes = BitConverter.ToUInt64(entry.Slice(48, 8)),
            Name = name
        };
    }

    public readonly void WriteTo(Span<byte> entry)
    {
        TypeGuid.ToByteArray().CopyTo(entry.Slice(0, 16));
        UniqueGuid.ToByteArray().CopyTo(entry.Slice(16, 16));
        BitConverter.TryWriteBytes(entry.Slice(32, 8), FirstLba);
        BitConverter.TryWriteBytes(entry.Slice(40, 8), LastLba);
        BitConverter.TryWriteBytes(entry.Slice(48, 8), Attributes);
        entry.Slice(56, 72).Clear();
        var nameBytes = Encoding.Unicode.GetBytes(Name ?? "");
        nameBytes.AsSpan(0, Math.Min(nameBytes.Length, 72)).CopyTo(entry.Slice(56, 72));
    }
}
