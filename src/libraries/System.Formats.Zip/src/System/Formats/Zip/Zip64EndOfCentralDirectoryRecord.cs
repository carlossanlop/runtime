// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.IO;
using System.IO.Compression;

namespace System.Formats.Zip;

internal struct Zip64EndOfCentralDirectoryRecord
{
    private const uint SignatureConstant = 0x06064B50;
    private const ulong NormalSize = 0x2C; // the size of the data excluding the size/signature fields if no extra data included

    public ulong SizeOfThisRecord;
    public ushort VersionMadeBy;
    public ushort VersionNeededToExtract;
    public uint NumberOfThisDisk;
    public uint NumberOfDiskWithStartOfCD;
    public ulong NumberOfEntriesOnThisDisk;
    public ulong NumberOfEntriesTotal;
    public ulong SizeOfCentralDirectory;
    public ulong OffsetOfCentralDirectory;

    public static bool TryReadBlock(BinaryReader reader, out Zip64EndOfCentralDirectoryRecord zip64EOCDRecord)
    {
        zip64EOCDRecord = default;

        if (reader.ReadUInt32() != SignatureConstant)
            return false;

        zip64EOCDRecord.SizeOfThisRecord = reader.ReadUInt64();
        zip64EOCDRecord.VersionMadeBy = reader.ReadUInt16();
        zip64EOCDRecord.VersionNeededToExtract = reader.ReadUInt16();
        zip64EOCDRecord.NumberOfThisDisk = reader.ReadUInt32();
        zip64EOCDRecord.NumberOfDiskWithStartOfCD = reader.ReadUInt32();
        zip64EOCDRecord.NumberOfEntriesOnThisDisk = reader.ReadUInt64();
        zip64EOCDRecord.NumberOfEntriesTotal = reader.ReadUInt64();
        zip64EOCDRecord.SizeOfCentralDirectory = reader.ReadUInt64();
        zip64EOCDRecord.OffsetOfCentralDirectory = reader.ReadUInt64();

        return true;
    }

    public static void WriteBlock(Stream stream, long numberOfEntries, long startOfCentralDirectory, long sizeOfCentralDirectory)
    {
        BinaryWriter writer = new BinaryWriter(stream);

        // write Zip 64 EOCD record
        writer.Write(SignatureConstant);
        writer.Write(NormalSize);
        writer.Write((ushort)ZipVersionNeededValues.Zip64); // version needed is 45 for zip 64 support
        writer.Write((ushort)ZipVersionNeededValues.Zip64); // version made by: high byte is 0 for MS DOS, low byte is version needed
        writer.Write((uint)0); // number of this disk is 0
        writer.Write((uint)0); // number of disk with start of central directory is 0
        writer.Write(numberOfEntries); // number of entries on this disk
        writer.Write(numberOfEntries); // number of entries total
        writer.Write(sizeOfCentralDirectory);
        writer.Write(startOfCentralDirectory);
    }
}
