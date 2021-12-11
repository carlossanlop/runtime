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
        internal byte[] _typeFlagBytes;
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

        internal bool TryReadCommonAttributeBytes(Stream archiveStream)
        {
            _nameBytes = new byte[FieldLengths.Name];
            if (archiveStream.Read(_nameBytes) != FieldLengths.Name)
            {
                return false;
            }
            _modeBytes = new byte[FieldLengths.Mode];
            if (archiveStream.Read(_modeBytes) != FieldLengths.Mode)
            {
                return false;
            }
            _uidBytes = new byte[FieldLengths.Uid];
            if (archiveStream.Read(_uidBytes) != FieldLengths.Uid)
            {
                return false;
            }
            _gidBytes = new byte[FieldLengths.Gid];
            if (archiveStream.Read(_gidBytes) != FieldLengths.Gid)
            {
                return false;
            }
            _sizeBytes = new byte[FieldLengths.Size];
            if (archiveStream.Read(_sizeBytes) != FieldLengths.Size)
            {
                return false;
            }
            _mTimeBytes = new byte[FieldLengths.MTime];
            if (archiveStream.Read(_mTimeBytes) != FieldLengths.MTime)
            {
                return false;
            }
            _checksumBytes = new byte[FieldLengths.Checksum];
            if (archiveStream.Read(_checksumBytes) != FieldLengths.Checksum)
            {
                return false;
            }
            // Empty checksum means this is an invalid (all blank) entry, finish early
            if (IsAllZeros(_checksumBytes))
            {
                return false;
            }

            _typeFlagBytes = new byte[FieldLengths.TypeFlag];
            if (archiveStream.Read(_typeFlagBytes) != FieldLengths.TypeFlag)
            {
                return false;
            }
            _linkNameBytes = new byte[FieldLengths.LinkName];
            if (archiveStream.Read(_linkNameBytes) != FieldLengths.LinkName)
            {
                return false;
            }

            return true;
        }

        private bool IsAllZeros(byte[] array)
        {
            for (int i = 0; i < array.Length; i++)
            {
                if (array[i] != 0)
                {
                    return false;
                }
            }
            return true;
        }

        internal bool TryReadMagicBytes(Stream archiveStream)
        {
            _magicBytes = new byte[FieldLengths.Magic];
            return archiveStream.Read(_magicBytes.AsSpan()) == FieldLengths.Magic;
        }

        internal bool TryReadPosixAndGnuSharedAttributeBytes(Stream archiveStream)
        {
            _versionBytes = new byte[FieldLengths.Version];
            if (archiveStream.Read(_versionBytes) != FieldLengths.Version)
            {
                return false;
            }
            _uNameBytes = new byte[FieldLengths.UName];
            if (archiveStream.Read(_uNameBytes) != FieldLengths.UName)
            {
                return false;
            }
            _gNameBytes = new byte[FieldLengths.GName];
            if (archiveStream.Read(_gNameBytes) != FieldLengths.GName)
            {
                return false;
            }
            _devMajorBytes = new byte[FieldLengths.DevMajor];
            if (archiveStream.Read(_devMajorBytes) != FieldLengths.DevMajor)
            {
                return false;
            }
            _devMinorBytes = new byte[FieldLengths.DevMinor];
            if (archiveStream.Read(_devMinorBytes) != FieldLengths.DevMinor)
            {
                return false;
            }

            return true;
        }

        internal bool TryReadPosixPrefixAttributeBytes(Stream archiveStream)
        {
            _prefixBytes = new byte[FieldLengths.Prefix];
            return archiveStream.Read(_prefixBytes.AsSpan()) == FieldLengths.Prefix;
        }

        internal bool TryReadV7PaddingBytes(Stream archiveStream) =>
            // Because we tried to detect the magic in case the header was ustar or above,
            // We already advanced those bytes, so we need to substract them from the expected
            // V7 padding length.
            TryReadPaddingBytes(archiveStream, FieldLengths.V7Padding - FieldLengths.Magic);

        internal bool TryReadPosixPaddingBytes(Stream archiveStream) =>
            TryReadPaddingBytes(archiveStream, FieldLengths.PosixPadding);

        private bool TryReadPaddingBytes(Stream archiveStream, ushort length)
        {
            _paddingBytes = new byte[length];
            return archiveStream.Read(_paddingBytes.AsSpan()) == length;
        }

        internal struct FieldLengths
        {
            private const ushort Path = 100;

            // Common attributes

            internal const ushort Name = Path;
            internal const ushort Mode = 8;
            internal const ushort Uid = 8;
            internal const ushort Gid = 8;
            internal const ushort Size = 12;
            internal const ushort MTime = 12;
            internal const ushort Checksum = 8;
            internal const ushort TypeFlag = 1;
            internal const ushort LinkName = Path;

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
