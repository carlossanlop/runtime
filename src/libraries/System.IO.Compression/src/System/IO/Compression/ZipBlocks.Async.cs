// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Buffers.Binary;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace System.IO.Compression;

// TODO: We might have to use classes for ZipBlocks in async code, as structs don't play well with async code.

internal partial struct ZipGenericExtraField
{
    public async Task WriteBlockAsync(Stream stream, CancellationToken cancellationToken)
    {
        byte[] extraFieldHeader = new byte[SizeOfHeader];

        BinaryPrimitives.WriteUInt16LittleEndian(extraFieldHeader.AsSpan(FieldLocations.Tag), _tag);
        BinaryPrimitives.WriteUInt16LittleEndian(extraFieldHeader.AsSpan(FieldLocations.Size), _size);

        await stream.WriteAsync(extraFieldHeader, cancellationToken).ConfigureAwait(false);
        await stream.WriteAsync(Data, cancellationToken).ConfigureAwait(false);
    }

    public static async Task WriteAllBlocksAsync(List<ZipGenericExtraField> fields, Stream stream, CancellationToken cancellationToken)
    {
        foreach (ZipGenericExtraField field in fields)
        {
            await field.WriteBlockAsync(stream, cancellationToken).ConfigureAwait(false);
        }
    }
}

internal partial struct Zip64ExtraField
{
    public async Task WriteBlockAsync(Stream stream, CancellationToken cancellationToken)
    {
        byte[] extraFieldData = new byte[TotalSize];
        int startOffset = ZipGenericExtraField.FieldLocations.DynamicData;

        BinaryPrimitives.WriteUInt16LittleEndian(extraFieldData.AsSpan(FieldLocations.Tag), TagConstant);
        BinaryPrimitives.WriteUInt16LittleEndian(extraFieldData.AsSpan(FieldLocations.Size), _size);

        if (_uncompressedSize != null)
        {
            BinaryPrimitives.WriteInt64LittleEndian(extraFieldData.AsSpan(startOffset), _uncompressedSize.Value);
            startOffset += FieldLengths.UncompressedSize;
        }

        if (_compressedSize != null)
        {
            BinaryPrimitives.WriteInt64LittleEndian(extraFieldData.AsSpan(startOffset), _compressedSize.Value);
            startOffset += FieldLengths.CompressedSize;
        }

        if (_localHeaderOffset != null)
        {
            BinaryPrimitives.WriteInt64LittleEndian(extraFieldData.AsSpan(startOffset), _localHeaderOffset.Value);
            startOffset += FieldLengths.LocalHeaderOffset;
        }

        if (_startDiskNumber != null)
        {
            BinaryPrimitives.WriteUInt32LittleEndian(extraFieldData.AsSpan(startOffset), _startDiskNumber.Value);
        }

        await stream.WriteAsync(extraFieldData, cancellationToken).ConfigureAwait(false);
    }
}

internal partial struct Zip64EndOfCentralDirectoryLocator
{
    public static async Task<(bool, Zip64EndOfCentralDirectoryLocator)> TryReadBlockAsync(Stream stream, CancellationToken cancellationToken)
    {
        Zip64EndOfCentralDirectoryLocator zip64EOCDLocator = default;
        byte[] blockContents = new byte[TotalSize];
        int bytesRead;

        bytesRead = await stream.ReadAsync(blockContents, cancellationToken).ConfigureAwait(false);

        if (bytesRead < TotalSize)
        {
            return (false, zip64EOCDLocator);
        }

        if (!blockContents.StartsWith(SignatureConstantBytes))
        {
            return (false, zip64EOCDLocator);
        }

        zip64EOCDLocator.NumberOfDiskWithZip64EOCD = BinaryPrimitives.ReadUInt32LittleEndian(blockContents.AsSpan(FieldLocations.NumberOfDiskWithZip64EOCD));
        zip64EOCDLocator.OffsetOfZip64EOCD = BinaryPrimitives.ReadUInt64LittleEndian(blockContents.AsSpan(FieldLocations.OffsetOfZip64EOCD));
        zip64EOCDLocator.TotalNumberOfDisks = BinaryPrimitives.ReadUInt32LittleEndian(blockContents.AsSpan(FieldLocations.TotalNumberOfDisks));

        return (true, zip64EOCDLocator);
    }

    public static async Task WriteBlockAsync(Stream stream, long zip64EOCDRecordStart, CancellationToken cancellationToken)
    {
        byte[] blockContents = new byte[TotalSize];

        SignatureConstantBytes.CopyTo(blockContents.AsSpan(FieldLocations.Signature));
        // number of disk with start of zip64 eocd
        BinaryPrimitives.WriteUInt32LittleEndian(blockContents.AsSpan(FieldLocations.NumberOfDiskWithZip64EOCD), 0);
        BinaryPrimitives.WriteInt64LittleEndian(blockContents.AsSpan(FieldLocations.OffsetOfZip64EOCD), zip64EOCDRecordStart);
        // total number of disks
        BinaryPrimitives.WriteUInt32LittleEndian(blockContents.AsSpan(FieldLocations.TotalNumberOfDisks), 1);

        await stream.WriteAsync(blockContents, cancellationToken).ConfigureAwait(false);
    }
}

internal partial struct Zip64EndOfCentralDirectoryRecord
{
    public static async Task<(bool, Zip64EndOfCentralDirectoryRecord)> TryReadBlockAsync(Stream stream, CancellationToken cancellationToken)
    {
        Zip64EndOfCentralDirectoryRecord zip64EOCDRecord = default;

        byte[] blockContents = new byte[BlockConstantSectionSize];
        int bytesRead;

        bytesRead = await stream.ReadAsync(blockContents, cancellationToken).ConfigureAwait(false);

        if (bytesRead < BlockConstantSectionSize)
        {
            return (false, zip64EOCDRecord);
        }

        if (!blockContents.StartsWith(SignatureConstantBytes))
        {
            return (false, zip64EOCDRecord);
        }

        zip64EOCDRecord.SizeOfThisRecord = BinaryPrimitives.ReadUInt64LittleEndian(blockContents.AsSpan(FieldLocations.SizeOfThisRecord));
        zip64EOCDRecord.VersionMadeBy = BinaryPrimitives.ReadUInt16LittleEndian(blockContents.AsSpan(FieldLocations.VersionMadeBy));
        zip64EOCDRecord.VersionNeededToExtract = BinaryPrimitives.ReadUInt16LittleEndian(blockContents.AsSpan(FieldLocations.VersionNeededToExtract));
        zip64EOCDRecord.NumberOfThisDisk = BinaryPrimitives.ReadUInt32LittleEndian(blockContents.AsSpan(FieldLocations.NumberOfThisDisk));
        zip64EOCDRecord.NumberOfDiskWithStartOfCD = BinaryPrimitives.ReadUInt32LittleEndian(blockContents.AsSpan(FieldLocations.NumberOfDiskWithStartOfCD));
        zip64EOCDRecord.NumberOfEntriesOnThisDisk = BinaryPrimitives.ReadUInt64LittleEndian(blockContents.AsSpan(FieldLocations.NumberOfEntriesOnThisDisk));
        zip64EOCDRecord.NumberOfEntriesTotal = BinaryPrimitives.ReadUInt64LittleEndian(blockContents.AsSpan(FieldLocations.NumberOfEntriesTotal));
        zip64EOCDRecord.SizeOfCentralDirectory = BinaryPrimitives.ReadUInt64LittleEndian(blockContents.AsSpan(FieldLocations.SizeOfCentralDirectory));
        zip64EOCDRecord.OffsetOfCentralDirectory = BinaryPrimitives.ReadUInt64LittleEndian(blockContents.AsSpan(FieldLocations.OffsetOfCentralDirectory));

        return (true, zip64EOCDRecord);
    }

    public static async Task WriteBlockAsync(Stream stream, long numberOfEntries, long startOfCentralDirectory, long sizeOfCentralDirectory, CancellationToken cancellationToken)
    {
        byte[] blockContents = new byte[BlockConstantSectionSize];

        SignatureConstantBytes.CopyTo(blockContents.AsSpan(FieldLocations.Signature));
        BinaryPrimitives.WriteUInt64LittleEndian(blockContents.AsSpan(FieldLocations.SizeOfThisRecord), NormalSize);
        // version made by: high byte is 0 for MS DOS, low byte is version needed
        BinaryPrimitives.WriteUInt16LittleEndian(blockContents.AsSpan(FieldLocations.VersionMadeBy), (ushort)ZipVersionNeededValues.Zip64);
        // version needed is 45 for zip 64 support
        BinaryPrimitives.WriteUInt16LittleEndian(blockContents.AsSpan(FieldLocations.VersionNeededToExtract), (ushort)ZipVersionNeededValues.Zip64);
        // number of this disk is 0
        BinaryPrimitives.WriteUInt32LittleEndian(blockContents.AsSpan(FieldLocations.NumberOfThisDisk), 0);
        // number of disk with start of central directory is 0
        BinaryPrimitives.WriteUInt32LittleEndian(blockContents.AsSpan(FieldLocations.NumberOfDiskWithStartOfCD), 0);
        // number of entries on this disk
        BinaryPrimitives.WriteInt64LittleEndian(blockContents.AsSpan(FieldLocations.NumberOfEntriesOnThisDisk), numberOfEntries);
        // number of entries total
        BinaryPrimitives.WriteInt64LittleEndian(blockContents.AsSpan(FieldLocations.NumberOfEntriesTotal), numberOfEntries);
        BinaryPrimitives.WriteInt64LittleEndian(blockContents.AsSpan(FieldLocations.SizeOfCentralDirectory), sizeOfCentralDirectory);
        BinaryPrimitives.WriteInt64LittleEndian(blockContents.AsSpan(FieldLocations.OffsetOfCentralDirectory), startOfCentralDirectory);

        // write Zip 64 EOCD record
        await stream.WriteAsync(blockContents, cancellationToken).ConfigureAwait(false);
    }
}

internal readonly partial struct ZipLocalFileHeader
{
    public static async Task<List<ZipGenericExtraField>> GetExtraFieldsAsync(Stream stream, CancellationToken cancellationToken)
    {
        // assumes that TrySkipBlock has already been called, so we don't have to validate twice

        List<ZipGenericExtraField> result;
        int relativeFilenameLengthLocation = FieldLocations.FilenameLength - FieldLocations.FilenameLength;
        int relativeExtraFieldLengthLocation = FieldLocations.ExtraFieldLength - FieldLocations.FilenameLength;
        byte[] fixedHeaderBuffer = new byte[FieldLengths.FilenameLength + FieldLengths.ExtraFieldLength];

        stream.Seek(FieldLocations.FilenameLength, SeekOrigin.Current);
        await stream.ReadExactlyAsync(fixedHeaderBuffer, cancellationToken).ConfigureAwait(false);

        ushort filenameLength = BinaryPrimitives.ReadUInt16LittleEndian(fixedHeaderBuffer.AsSpan(relativeFilenameLengthLocation));
        ushort extraFieldLength = BinaryPrimitives.ReadUInt16LittleEndian(fixedHeaderBuffer.AsSpan(relativeExtraFieldLengthLocation));
        byte[] extraFieldBuffer = Buffers.ArrayPool<byte>.Shared.Rent(extraFieldLength);

        try
        {
            stream.Seek(filenameLength, SeekOrigin.Current);
            await stream.ReadExactlyAsync(extraFieldBuffer, cancellationToken).ConfigureAwait(false);

            result = ZipGenericExtraField.ParseExtraField(extraFieldBuffer);
            Zip64ExtraField.RemoveZip64Blocks(result);

            return result;
        }
        finally
        {
            Buffers.ArrayPool<byte>.Shared.Return(extraFieldBuffer);
        }
    }

    // will not throw end of stream exception
    public static async Task<bool> TrySkipBlockAsync(Stream stream, CancellationToken cancellationToken)
    {
        byte[] blockBytes = new byte[4];
        long currPosition = stream.Position;
        int bytesRead = await stream.ReadAsync(blockBytes, cancellationToken).ConfigureAwait(false);

        if (bytesRead != FieldLengths.Signature || !blockBytes.SequenceEqual(SignatureConstantBytes))
        {
            return false;
        }

        if (stream.Length < currPosition + FieldLocations.FilenameLength)
        {
            return false;
        }

        // Already read the signature, so make the filename length field location relative to that
        stream.Seek(FieldLocations.FilenameLength - FieldLengths.Signature, SeekOrigin.Current);

        bytesRead = await stream.ReadAsync(blockBytes, cancellationToken).ConfigureAwait(false);
        if (bytesRead != FieldLengths.FilenameLength + FieldLengths.ExtraFieldLength)
        {
            return false;
        }

        int relativeFilenameLengthLocation = FieldLocations.FilenameLength - FieldLocations.FilenameLength;
        int relativeExtraFieldLengthLocation = FieldLocations.ExtraFieldLength - FieldLocations.FilenameLength;
        ushort filenameLength = BinaryPrimitives.ReadUInt16LittleEndian(blockBytes.AsSpan(relativeFilenameLengthLocation));
        ushort extraFieldLength = BinaryPrimitives.ReadUInt16LittleEndian(blockBytes.AsSpan(relativeExtraFieldLengthLocation));

        if (stream.Length < stream.Position + filenameLength + extraFieldLength)
        {
            return false;
        }

        stream.Seek(filenameLength + extraFieldLength, SeekOrigin.Current);

        return true;
    }
}

internal partial struct ZipCentralDirectoryFileHeader
{
    // if saveExtraFieldsAndComments is false, FileComment and ExtraFields will be null
    // in either case, the zip64 extra field info will be incorporated into other fields
    public static Task<(bool, int, ZipCentralDirectoryFileHeader)> TryReadBlockAsync(ReadOnlyMemory<byte> memory, Stream furtherReads, bool saveExtraFieldsAndComments)
    {
        const int StackAllocationThreshold = 512;

        ZipCentralDirectoryFileHeader header = default;
        int bytesRead = 0;

        ReadOnlySpan<byte> buffer = memory.Span;

        // the buffer will always be large enough for at least the constant section to be verified
        Debug.Assert(buffer.Length >= BlockConstantSectionSize);

        if (!buffer.StartsWith(SignatureConstantBytes))
        {
            return Task.FromResult((false, bytesRead, header));
        }

        header.VersionMadeBySpecification = buffer[FieldLocations.VersionMadeBySpecification];
        header.VersionMadeByCompatibility = buffer[FieldLocations.VersionMadeByCompatibility];
        header.VersionNeededToExtract = BinaryPrimitives.ReadUInt16LittleEndian(buffer[FieldLocations.VersionNeededToExtract..]);
        header.GeneralPurposeBitFlag = BinaryPrimitives.ReadUInt16LittleEndian(buffer[FieldLocations.GeneralPurposeBitFlags..]);
        header.CompressionMethod = BinaryPrimitives.ReadUInt16LittleEndian(buffer[FieldLocations.CompressionMethod..]);
        header.LastModified = BinaryPrimitives.ReadUInt32LittleEndian(buffer[FieldLocations.LastModified..]);
        header.Crc32 = BinaryPrimitives.ReadUInt32LittleEndian(buffer[FieldLocations.Crc32..]);

        uint compressedSizeSmall = BinaryPrimitives.ReadUInt32LittleEndian(buffer[FieldLocations.CompressedSize..]);
        uint uncompressedSizeSmall = BinaryPrimitives.ReadUInt32LittleEndian(buffer[FieldLocations.UncompressedSize..]);

        header.FilenameLength = BinaryPrimitives.ReadUInt16LittleEndian(buffer[FieldLocations.FilenameLength..]);
        header.ExtraFieldLength = BinaryPrimitives.ReadUInt16LittleEndian(buffer[FieldLocations.ExtraFieldLength..]);
        header.FileCommentLength = BinaryPrimitives.ReadUInt16LittleEndian(buffer[FieldLocations.FileCommentLength..]);

        ushort diskNumberStartSmall = BinaryPrimitives.ReadUInt16LittleEndian(buffer[FieldLocations.DiskNumberStart..]);

        header.InternalFileAttributes = BinaryPrimitives.ReadUInt16LittleEndian(buffer[FieldLocations.InternalFileAttributes..]);
        header.ExternalFileAttributes = BinaryPrimitives.ReadUInt32LittleEndian(buffer[FieldLocations.ExternalFileAttributes..]);

        uint relativeOffsetOfLocalHeaderSmall = BinaryPrimitives.ReadUInt32LittleEndian(buffer[FieldLocations.RelativeOffsetOfLocalHeader..]);

        // Assemble the dynamic header in a separate buffer. We can't guarantee that it's all in the input buffer,
        // some additional data might need to come from the stream.
        int dynamicHeaderSize = header.FilenameLength + header.ExtraFieldLength + header.FileCommentLength;
        int remainingBufferLength = buffer.Length - FieldLocations.DynamicData;
        int bytesToRead = dynamicHeaderSize - remainingBufferLength;
        scoped ReadOnlySpan<byte> dynamicHeader;
        byte[]? arrayPoolBuffer = null;

        Zip64ExtraField zip64;

        try
        {
            // No need to read extra data from the stream, no need to allocate a new buffer.
            if (bytesToRead <= 0)
            {
                dynamicHeader = buffer[FieldLocations.DynamicData..];
            }
            // Data needs to come from two sources, and we must thus copy data into a single address space.
            else
            {
                if (dynamicHeaderSize > StackAllocationThreshold)
                {
                    arrayPoolBuffer = Buffers.ArrayPool<byte>.Shared.Rent(dynamicHeaderSize);
                }

                Span<byte> collatedHeader = dynamicHeaderSize <= StackAllocationThreshold ? stackalloc byte[StackAllocationThreshold].Slice(0, dynamicHeaderSize) : arrayPoolBuffer.AsSpan(0, dynamicHeaderSize);

                buffer[FieldLocations.DynamicData..].CopyTo(collatedHeader);
                int realBytesRead = furtherReads.Read(collatedHeader[remainingBufferLength..]);

                if (realBytesRead != bytesToRead)
                {
                    return Task.FromResult((false, bytesRead, header));
                }
                dynamicHeader = collatedHeader;
            }

            header.Filename = dynamicHeader[..header.FilenameLength].ToArray();

            bool uncompressedSizeInZip64 = uncompressedSizeSmall == ZipHelper.Mask32Bit;
            bool compressedSizeInZip64 = compressedSizeSmall == ZipHelper.Mask32Bit;
            bool relativeOffsetInZip64 = relativeOffsetOfLocalHeaderSmall == ZipHelper.Mask32Bit;
            bool diskNumberStartInZip64 = diskNumberStartSmall == ZipHelper.Mask16Bit;

            ReadOnlySpan<byte> zipExtraFields = dynamicHeader.Slice(header.FilenameLength, header.ExtraFieldLength);

            zip64 = default;
            if (saveExtraFieldsAndComments)
            {
                header.ExtraFields = ZipGenericExtraField.ParseExtraField(zipExtraFields);
                zip64 = Zip64ExtraField.GetAndRemoveZip64Block(header.ExtraFields,
                            uncompressedSizeInZip64, compressedSizeInZip64,
                            relativeOffsetInZip64, diskNumberStartInZip64);
            }
            else
            {
                header.ExtraFields = null;
                zip64 = Zip64ExtraField.GetJustZip64Block(zipExtraFields,
                            uncompressedSizeInZip64, compressedSizeInZip64,
                            relativeOffsetInZip64, diskNumberStartInZip64);
            }

            header.FileComment = dynamicHeader.Slice(header.FilenameLength + header.ExtraFieldLength, header.FileCommentLength).ToArray();
        }
        finally
        {
            if (arrayPoolBuffer != null)
            {
                Buffers.ArrayPool<byte>.Shared.Return(arrayPoolBuffer);
            }
        }

        bytesRead = FieldLocations.DynamicData + dynamicHeaderSize;

        header.UncompressedSize = zip64.UncompressedSize ?? uncompressedSizeSmall;
        header.CompressedSize = zip64.CompressedSize ?? compressedSizeSmall;
        header.RelativeOffsetOfLocalHeader = zip64.LocalHeaderOffset ?? relativeOffsetOfLocalHeaderSmall;
        header.DiskNumberStart = zip64.StartDiskNumber ?? diskNumberStartSmall;

        return Task.FromResult((true, bytesRead, header));
    }
}

internal partial struct ZipEndOfCentralDirectoryBlock
{
    public static async Task WriteBlockAsync(Stream stream, long numberOfEntries, long startOfCentralDirectory, long sizeOfCentralDirectory, byte[] archiveComment, CancellationToken cancellationToken)
    {
        byte[] blockContents = new byte[TotalSize];

        ushort numberOfEntriesTruncated = numberOfEntries > ushort.MaxValue ?
                                                    ZipHelper.Mask16Bit : (ushort)numberOfEntries;
        uint startOfCentralDirectoryTruncated = startOfCentralDirectory > uint.MaxValue ?
                                                    ZipHelper.Mask32Bit : (uint)startOfCentralDirectory;
        uint sizeOfCentralDirectoryTruncated = sizeOfCentralDirectory > uint.MaxValue ?
                                                    ZipHelper.Mask32Bit : (uint)sizeOfCentralDirectory;

        SignatureConstantBytes.CopyTo(blockContents.AsSpan(FieldLocations.Signature));
        // number of this disk
        BinaryPrimitives.WriteUInt16LittleEndian(blockContents.AsSpan(FieldLocations.NumberOfThisDisk), 0);
        // number of disk with start of CD
        BinaryPrimitives.WriteUInt16LittleEndian(blockContents.AsSpan(FieldLocations.NumberOfTheDiskWithTheStartOfTheCentralDirectory), 0);
        // number of entries on this disk's cd
        BinaryPrimitives.WriteUInt16LittleEndian(blockContents.AsSpan(FieldLocations.NumberOfEntriesInTheCentralDirectoryOnThisDisk), numberOfEntriesTruncated);
        // number of entries in entire cd
        BinaryPrimitives.WriteUInt16LittleEndian(blockContents.AsSpan(FieldLocations.NumberOfEntriesInTheCentralDirectory), numberOfEntriesTruncated);
        BinaryPrimitives.WriteUInt32LittleEndian(blockContents.AsSpan(FieldLocations.SizeOfCentralDirectory), sizeOfCentralDirectoryTruncated);
        BinaryPrimitives.WriteUInt32LittleEndian(blockContents.AsSpan(FieldLocations.OffsetOfStartOfCentralDirectoryWithRespectToTheStartingDiskNumber), startOfCentralDirectoryTruncated);

        // Should be valid because of how we read archiveComment in TryReadBlock:
        Debug.Assert(archiveComment.Length <= ZipFileCommentMaxLength);

        // zip file comment length
        BinaryPrimitives.WriteUInt16LittleEndian(blockContents.AsSpan(FieldLocations.ArchiveCommentLength), (ushort)archiveComment.Length);

        await stream.WriteAsync(blockContents, cancellationToken).ConfigureAwait(false);
        if (archiveComment.Length > 0)
        {
            await stream.WriteAsync(archiveComment, cancellationToken).ConfigureAwait(false);
        }
    }

    public static async Task<(bool, ZipEndOfCentralDirectoryBlock)> TryReadBlockAsync(Stream stream, CancellationToken cancellationToken)
    {
        ZipEndOfCentralDirectoryBlock eocdBlock = default;
        byte[] blockContents = new byte[TotalSize];
        int bytesRead;

        bytesRead = await stream.ReadAsync(blockContents, cancellationToken).ConfigureAwait(false);

        if (bytesRead < TotalSize)
        {
            return (false, eocdBlock);
        }

        if (!blockContents.StartsWith(SignatureConstantBytes))
        {
            return (false, eocdBlock);
        }

        eocdBlock.Signature = BinaryPrimitives.ReadUInt32LittleEndian(blockContents.AsSpan(FieldLocations.Signature));
        eocdBlock.NumberOfThisDisk = BinaryPrimitives.ReadUInt16LittleEndian(blockContents.AsSpan(FieldLocations.NumberOfThisDisk));
        eocdBlock.NumberOfTheDiskWithTheStartOfTheCentralDirectory = BinaryPrimitives.ReadUInt16LittleEndian(blockContents.AsSpan(FieldLocations.NumberOfTheDiskWithTheStartOfTheCentralDirectory));
        eocdBlock.NumberOfEntriesInTheCentralDirectoryOnThisDisk = BinaryPrimitives.ReadUInt16LittleEndian(blockContents.AsSpan(FieldLocations.NumberOfEntriesInTheCentralDirectoryOnThisDisk));
        eocdBlock.NumberOfEntriesInTheCentralDirectory = BinaryPrimitives.ReadUInt16LittleEndian(blockContents.AsSpan(FieldLocations.NumberOfEntriesInTheCentralDirectory));
        eocdBlock.SizeOfCentralDirectory = BinaryPrimitives.ReadUInt32LittleEndian(blockContents.AsSpan(FieldLocations.SizeOfCentralDirectory));
        eocdBlock.OffsetOfStartOfCentralDirectoryWithRespectToTheStartingDiskNumber =
            BinaryPrimitives.ReadUInt32LittleEndian(blockContents.AsSpan(FieldLocations.OffsetOfStartOfCentralDirectoryWithRespectToTheStartingDiskNumber));

        ushort commentLength = BinaryPrimitives.ReadUInt16LittleEndian(blockContents.AsSpan(FieldLocations.ArchiveCommentLength));

        if (stream.Position + commentLength > stream.Length)
        {
            return (false, eocdBlock);
        }

        if (commentLength == 0)
        {
            eocdBlock.ArchiveComment = [];
        }
        else
        {
            eocdBlock.ArchiveComment = new byte[commentLength];
            await stream.ReadExactlyAsync(eocdBlock.ArchiveComment, cancellationToken).ConfigureAwait(false);
        }

        return (true, eocdBlock);
    }
}
