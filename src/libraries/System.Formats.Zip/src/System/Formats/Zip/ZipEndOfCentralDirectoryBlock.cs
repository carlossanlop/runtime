// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics;
using System.IO;
using System.IO.Compression;

namespace System.Formats.Zip;

internal struct ZipEndOfCentralDirectoryBlock
{
    public const uint SignatureConstant = 0x06054B50;
    public const int SignatureSize = sizeof(uint);

    // This is the minimum possible size, assuming the zip file comments variable section is empty
    public const int SizeOfBlockWithoutSignature = 18;

    // The end of central directory can have a variable size zip file comment at the end, but its max length can be 64K
    // The Zip File Format Specification does not explicitly mention a max size for this field, but we are assuming this
    // max size because that is the maximum value an ushort can hold.
    public const int ZipFileCommentMaxLength = ushort.MaxValue;

    public uint Signature;
    public ushort NumberOfThisDisk;
    public ushort NumberOfTheDiskWithTheStartOfTheCentralDirectory;
    public ushort NumberOfEntriesInTheCentralDirectoryOnThisDisk;
    public ushort NumberOfEntriesInTheCentralDirectory;
    public uint SizeOfCentralDirectory;
    public uint OffsetOfStartOfCentralDirectoryWithRespectToTheStartingDiskNumber;
    public byte[] ArchiveComment;

    public static void WriteBlock(Stream stream, long numberOfEntries, long startOfCentralDirectory, long sizeOfCentralDirectory, byte[] archiveComment)
    {
        BinaryWriter writer = new BinaryWriter(stream);

        ushort numberOfEntriesTruncated = numberOfEntries > ushort.MaxValue ?
                                                    ZipHelper.Mask16Bit : (ushort)numberOfEntries;
        uint startOfCentralDirectoryTruncated = startOfCentralDirectory > uint.MaxValue ?
                                                    ZipHelper.Mask32Bit : (uint)startOfCentralDirectory;
        uint sizeOfCentralDirectoryTruncated = sizeOfCentralDirectory > uint.MaxValue ?
                                                    ZipHelper.Mask32Bit : (uint)sizeOfCentralDirectory;

        writer.Write(SignatureConstant);
        writer.Write((ushort)0); // number of this disk
        writer.Write((ushort)0); // number of disk with start of CD
        writer.Write(numberOfEntriesTruncated); // number of entries on this disk's cd
        writer.Write(numberOfEntriesTruncated); // number of entries in entire CD
        writer.Write(sizeOfCentralDirectoryTruncated);
        writer.Write(startOfCentralDirectoryTruncated);

        // Should be valid because of how we read archiveComment in TryReadBlock:
        Debug.Assert(archiveComment.Length <= ZipFileCommentMaxLength);

        writer.Write((ushort)archiveComment.Length); // zip file comment length
        if (archiveComment.Length > 0)
            writer.Write(archiveComment);
    }

    public static bool TryReadBlock(BinaryReader reader, out ZipEndOfCentralDirectoryBlock eocdBlock)
    {
        eocdBlock = default;
        if (reader.ReadUInt32() != SignatureConstant)
            return false;

        eocdBlock.Signature = SignatureConstant;
        eocdBlock.NumberOfThisDisk = reader.ReadUInt16();
        eocdBlock.NumberOfTheDiskWithTheStartOfTheCentralDirectory = reader.ReadUInt16();
        eocdBlock.NumberOfEntriesInTheCentralDirectoryOnThisDisk = reader.ReadUInt16();
        eocdBlock.NumberOfEntriesInTheCentralDirectory = reader.ReadUInt16();
        eocdBlock.SizeOfCentralDirectory = reader.ReadUInt32();
        eocdBlock.OffsetOfStartOfCentralDirectoryWithRespectToTheStartingDiskNumber = reader.ReadUInt32();

        ushort commentLength = reader.ReadUInt16();
        eocdBlock.ArchiveComment = reader.ReadBytes(commentLength);

        return true;
    }
}
