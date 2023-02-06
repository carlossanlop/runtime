// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using System.IO;
using System.IO.Compression;

namespace System.Formats.Zip;

internal struct ZipCentralDirectoryFileHeader
{
    public const uint SignatureConstant = 0x02014B50;
    public byte VersionMadeByCompatibility;
    public byte VersionMadeBySpecification;
    public ushort VersionNeededToExtract;
    public ushort GeneralPurposeBitFlag;
    public ushort CompressionMethod;
    public uint LastModified; // convert this on the fly
    public uint Crc32;
    public long CompressedSize;
    public long UncompressedSize;
    public ushort FilenameLength;
    public ushort ExtraFieldLength;
    public ushort FileCommentLength;
    public int DiskNumberStart;
    public ushort InternalFileAttributes;
    public uint ExternalFileAttributes;
    public long RelativeOffsetOfLocalHeader;

    public byte[] Filename;
    public byte[] FileComment;
    public List<ZipGenericExtraField>? ExtraFields;

    // if saveExtraFieldsAndComments is false, FileComment and ExtraFields will be null
    // in either case, the zip64 extra field info will be incorporated into other fields
    public static bool TryReadBlock(BinaryReader reader, bool saveExtraFieldsAndComments, out ZipCentralDirectoryFileHeader header)
    {
        header = default;

        if (reader.ReadUInt32() != SignatureConstant)
            return false;
        header.VersionMadeBySpecification = reader.ReadByte();
        header.VersionMadeByCompatibility = reader.ReadByte();
        header.VersionNeededToExtract = reader.ReadUInt16();
        header.GeneralPurposeBitFlag = reader.ReadUInt16();
        header.CompressionMethod = reader.ReadUInt16();
        header.LastModified = reader.ReadUInt32();
        header.Crc32 = reader.ReadUInt32();
        uint compressedSizeSmall = reader.ReadUInt32();
        uint uncompressedSizeSmall = reader.ReadUInt32();
        header.FilenameLength = reader.ReadUInt16();
        header.ExtraFieldLength = reader.ReadUInt16();
        header.FileCommentLength = reader.ReadUInt16();
        ushort diskNumberStartSmall = reader.ReadUInt16();
        header.InternalFileAttributes = reader.ReadUInt16();
        header.ExternalFileAttributes = reader.ReadUInt32();
        uint relativeOffsetOfLocalHeaderSmall = reader.ReadUInt32();

        header.Filename = reader.ReadBytes(header.FilenameLength);

        bool uncompressedSizeInZip64 = uncompressedSizeSmall == ZipHelper.Mask32Bit;
        bool compressedSizeInZip64 = compressedSizeSmall == ZipHelper.Mask32Bit;
        bool relativeOffsetInZip64 = relativeOffsetOfLocalHeaderSmall == ZipHelper.Mask32Bit;
        bool diskNumberStartInZip64 = diskNumberStartSmall == ZipHelper.Mask16Bit;

        Zip64ExtraField zip64;

        long endExtraFields = reader.BaseStream.Position + header.ExtraFieldLength;
        using (Stream str = new SubReadStream(reader.BaseStream, reader.BaseStream.Position, header.ExtraFieldLength))
        {
            if (saveExtraFieldsAndComments)
            {
                header.ExtraFields = ZipGenericExtraField.ParseExtraField(str);
                zip64 = Zip64ExtraField.GetAndRemoveZip64Block(header.ExtraFields,
                        uncompressedSizeInZip64, compressedSizeInZip64,
                        relativeOffsetInZip64, diskNumberStartInZip64);
            }
            else
            {
                header.ExtraFields = null;
                zip64 = Zip64ExtraField.GetJustZip64Block(str,
                        uncompressedSizeInZip64, compressedSizeInZip64,
                        relativeOffsetInZip64, diskNumberStartInZip64);
            }
        }

        // There are zip files that have malformed ExtraField blocks in which GetJustZip64Block() silently bails out without reading all the way to the end
        // of the ExtraField block. Thus we must force the stream's position to the proper place.
        reader.BaseStream.AdvanceToPosition(endExtraFields);

        header.FileComment = reader.ReadBytes(header.FileCommentLength);

        header.UncompressedSize = zip64.UncompressedSize == null
                                                ? uncompressedSizeSmall
                                                : zip64.UncompressedSize.Value;
        header.CompressedSize = zip64.CompressedSize == null
                                                ? compressedSizeSmall
                                                : zip64.CompressedSize.Value;
        header.RelativeOffsetOfLocalHeader = zip64.LocalHeaderOffset == null
                                                ? relativeOffsetOfLocalHeaderSmall
                                                : zip64.LocalHeaderOffset.Value;
        header.DiskNumberStart = zip64.StartDiskNumber == null
                                                ? diskNumberStartSmall
                                                : zip64.StartDiskNumber.Value;

        return true;
    }
}
