// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace System.IO.Compression
{
    // Reads the tar stream and stores the fields as raw bytes.
    // Supported formats:
    // - Version 7 AT&T Unix (v7 for short). Oldest.
    // Documentation: https://www.freebsd.org/cgi/man.cgi?query=tar&sektion=5
    internal struct RawTarHeader
    {
        internal byte[] _nameBytes;
        internal byte[] _modeBytes;
        internal byte[] _uidBytes;
        internal byte[] _gidBytes;
        internal byte[] _sizeBytes;
        internal byte[] _mTimeBytes;
        internal byte[] _checksumBytes;
        internal byte _typeFlagBytes;
        internal byte[] _linkNameBytes;
        internal byte[] _paddingBytes;

        internal void ReadCommonAttributeBytes(BinaryReader reader)
        {
            _nameBytes = reader.ReadBytes(FieldSizes.Name);
            _modeBytes = reader.ReadBytes(FieldSizes.Mode);
            _uidBytes = reader.ReadBytes(FieldSizes.Uid);
            _gidBytes = reader.ReadBytes(FieldSizes.Gid);
            _sizeBytes = reader.ReadBytes(FieldSizes.Size);
            _mTimeBytes = reader.ReadBytes(FieldSizes.MTime);
            _checksumBytes = reader.ReadBytes(FieldSizes.CheckSum);
            _typeFlagBytes = reader.ReadByte();
            _linkNameBytes = reader.ReadBytes(FieldSizes.LinkName);
        }

        internal void ReadV7PaddingBytes(BinaryReader reader)
        {
            ReadPaddingBytes(reader, FieldSizes.V7Padding);
        }

        private void ReadPaddingBytes(BinaryReader reader, ushort length)
        {
            _paddingBytes = reader.ReadBytes(length);
        }
        internal struct FieldSizes
        {
            private const ushort PathLength = 100;

            internal const ushort Name = PathLength;
            internal const ushort Mode = 8;
            internal const ushort Uid = 8;
            internal const ushort Gid = 8;
            internal const ushort Size = 12;
            internal const ushort MTime = 12;
            internal const ushort CheckSum = 8;
            internal const ushort LinkName = PathLength;

            internal const ushort V7Padding = 255;
        }
    }
}
