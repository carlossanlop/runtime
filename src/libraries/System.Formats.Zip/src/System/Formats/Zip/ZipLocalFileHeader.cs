// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using System.IO.Compression;
using System.IO;

namespace System.Formats.Zip;

internal readonly struct ZipLocalFileHeader
{
    public const uint DataDescriptorSignature = 0x08074B50;
    public const uint SignatureConstant = 0x04034B50;
    public const int OffsetToCrcFromHeaderStart = 14;
    public const int OffsetToVersionFromHeaderStart = 4;
    public const int OffsetToBitFlagFromHeaderStart = 6;
    public const int SizeOfLocalHeader = 30;

    public static List<ZipGenericExtraField> GetExtraFields(BinaryReader reader)
    {
        // assumes that TrySkipBlock has already been called, so we don't have to validate twice

        List<ZipGenericExtraField> result;

        const int OffsetToFilenameLength = 26; // from the point before the signature

        reader.BaseStream.Seek(OffsetToFilenameLength, SeekOrigin.Current);

        ushort filenameLength = reader.ReadUInt16();
        ushort extraFieldLength = reader.ReadUInt16();

        reader.BaseStream.Seek(filenameLength, SeekOrigin.Current);


        using (Stream str = new SubReadStream(reader.BaseStream, reader.BaseStream.Position, extraFieldLength))
        {
            result = ZipGenericExtraField.ParseExtraField(str);
        }
        Zip64ExtraField.RemoveZip64Blocks(result);

        return result;
    }

    // will not throw end of stream exception
    public static bool TrySkipBlock(BinaryReader reader)
    {
        const int OffsetToFilenameLength = 22; // from the point after the signature

        if (reader.ReadUInt32() != SignatureConstant)
            return false;


        if (reader.BaseStream.Length < reader.BaseStream.Position + OffsetToFilenameLength)
            return false;

        reader.BaseStream.Seek(OffsetToFilenameLength, SeekOrigin.Current);

        ushort filenameLength = reader.ReadUInt16();
        ushort extraFieldLength = reader.ReadUInt16();

        if (reader.BaseStream.Length < reader.BaseStream.Position + filenameLength + extraFieldLength)
            return false;

        reader.BaseStream.Seek(filenameLength + extraFieldLength, SeekOrigin.Current);

        return true;
    }
}
