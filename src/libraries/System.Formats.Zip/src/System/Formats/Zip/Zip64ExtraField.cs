// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace System.Formats.Zip;

internal struct Zip64ExtraField
{
    // Size is size of the record not including the tag or size fields
    // If the extra field is going in the local header, it cannot include only
    // one of uncompressed/compressed size

    public const int OffsetToFirstField = 4;
    private const ushort TagConstant = 1;

    private ushort _size;
    private long? _uncompressedSize;
    private long? _compressedSize;
    private long? _localHeaderOffset;
    private int? _startDiskNumber;

    public ushort TotalSize => (ushort)(_size + 4);

    public long? UncompressedSize
    {
        get { return _uncompressedSize; }
        set { _uncompressedSize = value; UpdateSize(); }
    }
    public long? CompressedSize
    {
        get { return _compressedSize; }
        set { _compressedSize = value; UpdateSize(); }
    }
    public long? LocalHeaderOffset
    {
        get { return _localHeaderOffset; }
        set { _localHeaderOffset = value; UpdateSize(); }
    }
    public int? StartDiskNumber => _startDiskNumber;

    private void UpdateSize()
    {
        _size = 0;
        if (_uncompressedSize != null) _size += 8;
        if (_compressedSize != null) _size += 8;
        if (_localHeaderOffset != null) _size += 8;
        if (_startDiskNumber != null) _size += 4;
    }

    // There is a small chance that something very weird could happen here. The code calling into this function
    // will ask for a value from the extra field if the field was masked with FF's. It's theoretically possible
    // that a field was FF's legitimately, and the writer didn't decide to write the corresponding extra field.
    // Also, at the same time, other fields were masked with FF's to indicate looking in the zip64 record.
    // Then, the search for the zip64 record will fail because the expected size is wrong,
    // and a nulled out Zip64ExtraField will be returned. Thus, even though there was Zip64 data,
    // it will not be used. It is questionable whether this situation is possible to detect
    // unlike the other functions that have try-pattern semantics, these functions always return a
    // Zip64ExtraField. If a Zip64 extra field actually doesn't exist, all of the fields in the
    // returned struct will be null
    //
    // If there are more than one Zip64 extra fields, we take the first one that has the expected size
    //
    public static Zip64ExtraField GetJustZip64Block(Stream extraFieldStream,
        bool readUncompressedSize, bool readCompressedSize,
        bool readLocalHeaderOffset, bool readStartDiskNumber)
    {
        Zip64ExtraField zip64Field;
        using (BinaryReader reader = new BinaryReader(extraFieldStream))
        {
            ZipGenericExtraField currentExtraField;
            while (ZipGenericExtraField.TryReadBlock(reader, extraFieldStream.Length, out currentExtraField))
            {
                if (TryGetZip64BlockFromGenericExtraField(currentExtraField, readUncompressedSize,
                            readCompressedSize, readLocalHeaderOffset, readStartDiskNumber, out zip64Field))
                {
                    return zip64Field;
                }
            }
        }

        zip64Field = default;

        zip64Field._compressedSize = null;
        zip64Field._uncompressedSize = null;
        zip64Field._localHeaderOffset = null;
        zip64Field._startDiskNumber = null;

        return zip64Field;
    }

    private static bool TryGetZip64BlockFromGenericExtraField(ZipGenericExtraField extraField,
        bool readUncompressedSize, bool readCompressedSize,
        bool readLocalHeaderOffset, bool readStartDiskNumber,
        out Zip64ExtraField zip64Block)
    {
        zip64Block = default;

        zip64Block._compressedSize = null;
        zip64Block._uncompressedSize = null;
        zip64Block._localHeaderOffset = null;
        zip64Block._startDiskNumber = null;

        if (extraField.Tag != TagConstant)
            return false;

        zip64Block._size = extraField.Size;

        using (MemoryStream ms = new MemoryStream(extraField.Data))
        using (BinaryReader reader = new BinaryReader(ms))
        {
            // The spec section 4.5.3:
            //      The order of the fields in the zip64 extended
            //      information record is fixed, but the fields MUST
            //      only appear if the corresponding Local or Central
            //      directory record field is set to 0xFFFF or 0xFFFFFFFF.
            // However tools commonly write the fields anyway; the prevailing convention
            // is to respect the size, but only actually use the values if their 32 bit
            // values were all 0xFF.

            if (extraField.Size < sizeof(long))
                return true;

            // Advancing the stream (by reading from it) is possible only when:
            // 1. There is an explicit ask to do that (valid files, corresponding boolean flag(s) set to true).
            // 2. When the size indicates that all the information is available ("slightly invalid files").
            bool readAllFields = extraField.Size >= sizeof(long) + sizeof(long) + sizeof(long) + sizeof(int);

            if (readUncompressedSize)
            {
                zip64Block._uncompressedSize = reader.ReadInt64();
            }
            else if (readAllFields)
            {
                _ = reader.ReadInt64();
            }

            if (ms.Position > extraField.Size - sizeof(long))
                return true;

            if (readCompressedSize)
            {
                zip64Block._compressedSize = reader.ReadInt64();
            }
            else if (readAllFields)
            {
                _ = reader.ReadInt64();
            }

            if (ms.Position > extraField.Size - sizeof(long))
                return true;

            if (readLocalHeaderOffset)
            {
                zip64Block._localHeaderOffset = reader.ReadInt64();
            }
            else if (readAllFields)
            {
                _ = reader.ReadInt64();
            }

            if (ms.Position > extraField.Size - sizeof(int))
                return true;

            if (readStartDiskNumber)
            {
                zip64Block._startDiskNumber = reader.ReadInt32();
            }
            else if (readAllFields)
            {
                _ = reader.ReadInt32();
            }

            // original values are unsigned, so implies value is too big to fit in signed integer
            if (zip64Block._uncompressedSize < 0) throw new InvalidDataException(SR.FieldTooBigUncompressedSize);
            if (zip64Block._compressedSize < 0) throw new InvalidDataException(SR.FieldTooBigCompressedSize);
            if (zip64Block._localHeaderOffset < 0) throw new InvalidDataException(SR.FieldTooBigLocalHeaderOffset);
            if (zip64Block._startDiskNumber < 0) throw new InvalidDataException(SR.FieldTooBigStartDiskNumber);

            return true;
        }
    }

    public static Zip64ExtraField GetAndRemoveZip64Block(List<ZipGenericExtraField> extraFields,
        bool readUncompressedSize, bool readCompressedSize,
        bool readLocalHeaderOffset, bool readStartDiskNumber)
    {
        Zip64ExtraField zip64Field = default;

        zip64Field._compressedSize = null;
        zip64Field._uncompressedSize = null;
        zip64Field._localHeaderOffset = null;
        zip64Field._startDiskNumber = null;

        List<ZipGenericExtraField> markedForDelete = new List<ZipGenericExtraField>();
        bool zip64FieldFound = false;

        foreach (ZipGenericExtraField ef in extraFields)
        {
            if (ef.Tag == TagConstant)
            {
                markedForDelete.Add(ef);
                if (!zip64FieldFound)
                {
                    if (TryGetZip64BlockFromGenericExtraField(ef, readUncompressedSize, readCompressedSize,
                                readLocalHeaderOffset, readStartDiskNumber, out zip64Field))
                    {
                        zip64FieldFound = true;
                    }
                }
            }
        }

        foreach (ZipGenericExtraField ef in markedForDelete)
            extraFields.Remove(ef);

        return zip64Field;
    }

    public static void RemoveZip64Blocks(List<ZipGenericExtraField> extraFields)
    {
        List<ZipGenericExtraField> markedForDelete = new List<ZipGenericExtraField>();
        foreach (ZipGenericExtraField field in extraFields)
            if (field.Tag == TagConstant)
                markedForDelete.Add(field);

        foreach (ZipGenericExtraField field in markedForDelete)
            extraFields.Remove(field);
    }

    public void WriteBlock(Stream stream)
    {
        BinaryWriter writer = new BinaryWriter(stream);
        writer.Write(TagConstant);
        writer.Write(_size);
        if (_uncompressedSize != null) writer.Write(_uncompressedSize.Value);
        if (_compressedSize != null) writer.Write(_compressedSize.Value);
        if (_localHeaderOffset != null) writer.Write(_localHeaderOffset.Value);
        if (_startDiskNumber != null) writer.Write(_startDiskNumber.Value);
    }
}
