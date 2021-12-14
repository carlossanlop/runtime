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
        internal byte[] _typeFlagBytes; // TODO: this should just be 1 byte
        internal byte[] _linkNameBytes;

        // POSIX and GNU shared attributes

        internal byte[] _magicBytes;

        internal byte[] _versionBytes;
        internal byte[] _uNameBytes;
        internal byte[] _gNameBytes;
        internal byte[] _devMajorBytes;
        internal byte[] _devMinorBytes;
        internal byte[] _paddingBytes; // TODO: This can be removed

        // POSIX attributes

        internal byte[] _prefixBytes;

        // GNU attributes

        internal byte[] _atimeBytes;
        internal byte[] _ctimeBytes;
        internal byte[] _offsetBytes;
        internal byte[] _longNameBytes;
        internal byte[] _unusedByte;
        internal struct Sparse
        {
            internal byte[] _offsetBytes;
            internal byte[] _byteNumberBytes;
        }
        internal Sparse[] _sparseItems;
        internal byte[] _isExtendedByte;
        internal byte[] _realSizeBytes;

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
            if (TarHeader.IsAllZeros(_checksumBytes))
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

        internal bool TryReadMagicBytes(Stream archiveStream)
        {
            _magicBytes = new byte[FieldLengths.Magic];
            return archiveStream.Read(_magicBytes.AsSpan()) == FieldLengths.Magic;
        }
        internal bool TryReadVersionBytes(Stream archiveStream)
        {
            _versionBytes = new byte[FieldLengths.Version];
            return archiveStream.Read(_versionBytes.AsSpan()) == FieldLengths.Version;
        }

        internal bool TryReadPosixAndGnuSharedAttributeBytes(Stream archiveStream)
        {
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

        internal bool TryReadGnuAttributeBytes(Stream archiveStream)
        {
            _atimeBytes = new byte[FieldLengths.ATime];
            if (archiveStream.Read(_atimeBytes) != FieldLengths.ATime)
            {
                return false;
            }
            _ctimeBytes = new byte[FieldLengths.CTime];
            if (archiveStream.Read(_ctimeBytes) != FieldLengths.CTime)
            {
                return false;
            }
            _offsetBytes = new byte[FieldLengths.Offset];
            if (archiveStream.Read(_offsetBytes) != FieldLengths.Offset)
            {
                return false;
            }
            _longNameBytes = new byte[FieldLengths.LongNames];
            if (archiveStream.Read(_longNameBytes) != FieldLengths.LongNames)
            {
                return false;
            }
            _unusedByte = new byte[FieldLengths.Unused];
            if (archiveStream.Read(_unusedByte) != FieldLengths.Unused)
            {
                return false;
            }
            _sparseItems = new Sparse[FieldLengths.SparseItems];

            for (int i = 0; i < FieldLengths.SparseItems; i++)
            {
                _sparseItems[0]._offsetBytes = new byte[FieldLengths.SparseOffset];
                if (archiveStream.Read(_sparseItems[0]._offsetBytes) != FieldLengths.SparseOffset)
                {
                    return false;
                }
                _sparseItems[0]._byteNumberBytes = new byte[FieldLengths.SparseByteNumber];
                if (archiveStream.Read(_sparseItems[0]._byteNumberBytes) != FieldLengths.SparseByteNumber)
                {
                    return false;
                }
            }

            _isExtendedByte = new byte[FieldLengths.IsExtended];
            if (archiveStream.Read(_isExtendedByte) != FieldLengths.IsExtended)
            {
                return false;
            }
            _realSizeBytes = new byte[FieldLengths.RealSize];
            if (archiveStream.Read(_realSizeBytes) != FieldLengths.RealSize)
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

        internal bool TryReadPosixPaddingBytes(Stream archiveStream) => TryReadPaddingBytes(archiveStream, FieldLengths.PosixPadding);

        internal bool TryReadGnuPaddingBytes(Stream archiveStream) => TryReadPaddingBytes(archiveStream, FieldLengths.GnuPadding);

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

            // GNU attributes

            internal const ushort ATime = 12;
            internal const ushort CTime = 12;
            internal const ushort Offset = 12;
            internal const ushort LongNames = 4;
            internal const ushort Unused = 1;
            internal const ushort SparseItems = 4;
            internal const ushort SparseOffset = 12;
            internal const ushort SparseByteNumber = 12;
            internal const ushort IsExtended = 1;
            internal const ushort RealSize = 12;

            // Padding lengths depending on format

            internal const ushort V7Padding = 255;
            internal const ushort PosixPadding = 12;
            internal const ushort GnuPadding = 17;
        }
    }
}
