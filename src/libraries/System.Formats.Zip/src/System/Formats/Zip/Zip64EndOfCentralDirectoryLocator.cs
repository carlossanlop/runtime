// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.IO;

namespace System.Formats.Zip;

internal struct Zip64EndOfCentralDirectoryLocator
{
    public const uint SignatureConstant = 0x07064B50;
    public const int SignatureSize = sizeof(uint);

    public const int SizeOfBlockWithoutSignature = 16;

    public uint NumberOfDiskWithZip64EOCD;
    public ulong OffsetOfZip64EOCD;
    public uint TotalNumberOfDisks;

    public static bool TryReadBlock(BinaryReader reader, out Zip64EndOfCentralDirectoryLocator zip64EOCDLocator)
    {
        zip64EOCDLocator = default;

        if (reader.ReadUInt32() != SignatureConstant)
            return false;

        zip64EOCDLocator.NumberOfDiskWithZip64EOCD = reader.ReadUInt32();
        zip64EOCDLocator.OffsetOfZip64EOCD = reader.ReadUInt64();
        zip64EOCDLocator.TotalNumberOfDisks = reader.ReadUInt32();
        return true;
    }

    public static void WriteBlock(Stream stream, long zip64EOCDRecordStart)
    {
        BinaryWriter writer = new BinaryWriter(stream);
        writer.Write(SignatureConstant);
        writer.Write((uint)0); // number of disk with start of zip64 eocd
        writer.Write(zip64EOCDRecordStart);
        writer.Write((uint)1); // total number of disks
    }
}
