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
        // Common attributes

        internal byte[] _nameBytes;
        internal byte[] _modeBytes;
        internal byte[] _uidBytes;
        internal byte[] _gidBytes;
        internal byte[] _sizeBytes;
        internal byte[] _mTimeBytes;
        internal byte[] _checksumBytes;
        internal byte _typeFlagBytes;
        internal byte[] _linkNameBytes;

        // POSIX and GNU shared attributes

        internal byte[] _magicBytes;
        internal byte[] _versionBytes;
        internal byte[] _uNameBytes;
        internal byte[] _gNameBytes;
        internal byte[] _devMajorBytes;
        internal byte[] _devMinorBytes;
        internal byte[] _paddingBytes;

        // POSIX attributes

        internal byte[] _prefixBytes;

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

        internal void ReadMagicBytes(BinaryReader reader)
        {
            _magicBytes = reader.ReadBytes(FieldSizes.Magic);
        }

        internal void ReadPosixAndGnuSharedAttributeBytes(BinaryReader reader)
        {
            _versionBytes = reader.ReadBytes(FieldSizes.Version);
            _uNameBytes = reader.ReadBytes(FieldSizes.UName);
            _gNameBytes = reader.ReadBytes(FieldSizes.GName);
            _devMajorBytes = reader.ReadBytes(FieldSizes.DevMajor);
            _devMinorBytes = reader.ReadBytes(FieldSizes.DevMinor);
        }

        internal void ReadPosixPrefixAttributeBytes(BinaryReader reader)
        {
            _prefixBytes = reader.ReadBytes(FieldSizes.Prefix);
        }

        internal void ReadV7PaddingBytes(BinaryReader reader)
        {
            // Because we tried to detect the magic in case the header was ustar or above,
            // We already advanced those bytes, so we need to substract them from the expected
            // V7 padding length.
            ReadPaddingBytes(reader, FieldSizes.V7Padding - FieldSizes.Magic);
        }

        internal void ReadPosixPaddingBytes(BinaryReader reader)
        {
            ReadPaddingBytes(reader, FieldSizes.PosixPadding);
        }

        private void ReadPaddingBytes(BinaryReader reader, ushort length)
        {
            _paddingBytes = reader.ReadBytes(length);
        }

        internal struct FieldSizes
        {
            private const ushort PathLength = 100;

            // Common attributes

            internal const ushort Name = PathLength;
            internal const ushort Mode = 8;
            internal const ushort Uid = 8;
            internal const ushort Gid = 8;
            internal const ushort Size = 12;
            internal const ushort MTime = 12;
            internal const ushort CheckSum = 8;
            internal const ushort LinkName = PathLength;

            // POSIX and GNU shared attributes

            internal const ushort Magic = 6;
            internal const ushort Version = 2;
            internal const ushort UName = 32;
            internal const ushort GName = 32;
            internal const ushort DevMajor = 8;
            internal const ushort DevMinor = 8;

            // POSIX attributes

            internal const ushort Prefix = 155;

            // Padding lengths depending on format

            internal const ushort V7Padding = 255;
            internal const ushort PosixPadding = 12;
        }
    }
}
