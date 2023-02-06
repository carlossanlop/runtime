// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using System.IO;

namespace System.Formats.Zip;

internal struct ZipGenericExtraField
{
    private const int SizeOfHeader = 4;

    private ushort _tag;
    private ushort _size;
    private byte[] _data;

    public ushort Tag => _tag;
    // returns size of data, not of the entire block
    public ushort Size => _size;
    public byte[] Data => _data;

    public void WriteBlock(Stream stream)
    {
        BinaryWriter writer = new BinaryWriter(stream);
        writer.Write(Tag);
        writer.Write(Size);
        writer.Write(Data);
    }

    // shouldn't ever read the byte at position endExtraField
    // assumes we are positioned at the beginning of an extra field subfield
    public static bool TryReadBlock(BinaryReader reader, long endExtraField, out ZipGenericExtraField field)
    {
        field = default;

        // not enough bytes to read tag + size
        if (endExtraField - reader.BaseStream.Position < 4)
            return false;

        field._tag = reader.ReadUInt16();
        field._size = reader.ReadUInt16();

        // not enough bytes to read the data
        if (endExtraField - reader.BaseStream.Position < field._size)
            return false;

        field._data = reader.ReadBytes(field._size);
        return true;
    }

    // shouldn't ever read the byte at position endExtraField
    public static List<ZipGenericExtraField> ParseExtraField(Stream extraFieldData)
    {
        List<ZipGenericExtraField> extraFields = new List<ZipGenericExtraField>();

        using (BinaryReader reader = new BinaryReader(extraFieldData))
        {
            ZipGenericExtraField field;
            while (TryReadBlock(reader, extraFieldData.Length, out field))
            {
                extraFields.Add(field);
            }
        }

        return extraFields;
    }

    public static int TotalSize(List<ZipGenericExtraField> fields)
    {
        int size = 0;
        foreach (ZipGenericExtraField field in fields)
            size += field.Size + SizeOfHeader; //size is only size of data
        return size;
    }

    public static void WriteAllBlocks(List<ZipGenericExtraField> fields, Stream stream)
    {
        foreach (ZipGenericExtraField field in fields)
            field.WriteBlock(stream);
    }
}
