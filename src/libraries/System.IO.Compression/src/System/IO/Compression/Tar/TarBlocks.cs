// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace System.IO.Compression.Tar
{
    // Reads the tar stream and stores the fields as raw bytes.
    // Supported formats:
    // - 1979 Version 7 AT&T Unix Tar Command Format (v7).
    // - POSIX IEEE 1003.1-1988 Unix Standard Tar Format (ustar).
    // - POSIX IEEE 1003.1-2001 ("POSIX.1") Pax Interchange Tar Format (pax).
    // - GNU Tar Format (gnu).
    // Documentation: https://www.freebsd.org/cgi/man.cgi?query=tar&sektion=5
    internal struct TarBlocks
    {
        // Common attributes

        internal byte[] _nameBytes;
        internal byte[] _modeBytes;
        internal byte[] _uidBytes;
        internal byte[] _gidBytes;
        internal byte[] _sizeBytes;
        internal byte[] _mTimeBytes;
        internal byte[] _checksumBytes;
        internal byte[] _typeFlagByte;
        internal byte[] _linkNameBytes;

        // POSIX and GNU shared attributes

        internal byte[] _magicBytes;

        internal byte[] _versionBytes;
        internal byte[] _uNameBytes;
        internal byte[] _gNameBytes;
        internal byte[] _devMajorBytes;
        internal byte[] _devMinorBytes;

        // POSIX attributes

        internal byte[] _prefixBytes;

        // GNU attributes

        internal byte[] _atimeBytes;
        internal byte[] _ctimeBytes;
        internal byte[] _offsetBytes;
        internal byte[] _longNameBytes;
        internal byte[] _unusedByte;

        // Thes sparse field is actually an array with 4 instances
        // of the Sparse struct, each containing two 12 byte arrays:
        // struct Sparse { byte[12] offset, byte[12] numbytes }
        internal byte[] _sparseBytes;
        internal byte[] _isExtendedByte;
        internal byte[] _realSizeBytes;

        // Reads and stores bytes of fields found in all formats.
        // Throws if end of stream is reached.
        // Returns true if everything was read successfully.
        // Returns false if the checksum is empty.
        internal bool TryReadCommonAttributeBytes(Stream archiveStream)
        {
            _nameBytes = new byte[FieldLengths.Name];
            TarHelpers.ReadOrThrow(archiveStream, ref _nameBytes, FieldLengths.Name);

            _modeBytes = new byte[FieldLengths.Mode];
            TarHelpers.ReadOrThrow(archiveStream, ref _modeBytes, FieldLengths.Mode);

            _uidBytes = new byte[FieldLengths.Uid];
            TarHelpers.ReadOrThrow(archiveStream, ref _uidBytes, FieldLengths.Uid);

            _gidBytes = new byte[FieldLengths.Gid];
            TarHelpers.ReadOrThrow(archiveStream, ref _gidBytes, FieldLengths.Gid);

            _sizeBytes = new byte[FieldLengths.Size];
            TarHelpers.ReadOrThrow(archiveStream, ref _sizeBytes, FieldLengths.Size);

            _mTimeBytes = new byte[FieldLengths.MTime];
            TarHelpers.ReadOrThrow(archiveStream, ref _mTimeBytes, FieldLengths.MTime);

            _checksumBytes = new byte[FieldLengths.Checksum];
            TarHelpers.ReadOrThrow(archiveStream, ref _checksumBytes, FieldLengths.Checksum);

            // Empty checksum means this is an invalid (all blank) entry, finish early
            if (TarHelpers.IsAllNullBytes(_checksumBytes))
            {
                return false;
            }

            _typeFlagByte = new byte[FieldLengths.TypeFlag];
            TarHelpers.ReadOrThrow(archiveStream, ref _typeFlagByte, FieldLengths.TypeFlag);

            _linkNameBytes = new byte[FieldLengths.LinkName];
            TarHelpers.ReadOrThrow(archiveStream, ref _linkNameBytes, FieldLengths.LinkName);

            return true;
        }

        // Reads and stores the bytes of the magic field.
        // Throws if the end of stream is reached.
        internal void ReadMagicBytes(Stream archiveStream)
        {
            _magicBytes = new byte[FieldLengths.Magic];
            TarHelpers.ReadOrThrow(archiveStream, ref _magicBytes, FieldLengths.Magic);
        }

        // Reads and stores the bytes of the version field.
        // Throws if the end of stream is reached.
        internal void ReadVersionBytes(Stream archiveStream)
        {
            _versionBytes = new byte[FieldLengths.Version];
            TarHelpers.ReadOrThrow(archiveStream, ref _versionBytes, FieldLengths.Version);
        }

        // Reads and stores bytes of fields found in the POSIX and GNU formats.
        // Throws if end of stream is reached.
        internal void ReadPosixAndGnuSharedAttributeBytes(Stream archiveStream)
        {
            _uNameBytes = new byte[FieldLengths.UName];
            TarHelpers.ReadOrThrow(archiveStream, ref _uNameBytes, FieldLengths.UName);

            _gNameBytes = new byte[FieldLengths.GName];
            TarHelpers.ReadOrThrow(archiveStream, ref _gNameBytes, FieldLengths.GName);

            _devMajorBytes = new byte[FieldLengths.DevMajor];
            TarHelpers.ReadOrThrow(archiveStream, ref _devMajorBytes, FieldLengths.DevMajor);

            _devMinorBytes = new byte[FieldLengths.DevMinor];
            TarHelpers.ReadOrThrow(archiveStream, ref _devMinorBytes, FieldLengths.DevMinor);
        }

        // Reads and stores bytes of fields found in the GNU format.
        // Throws if end of stream is reached.
        internal void ReadGnuAttributeBytes(Stream archiveStream)
        {
            _atimeBytes = new byte[FieldLengths.ATime];
            TarHelpers.ReadOrThrow(archiveStream, ref _atimeBytes, FieldLengths.ATime);

            _ctimeBytes = new byte[FieldLengths.CTime];
            TarHelpers.ReadOrThrow(archiveStream, ref _ctimeBytes, FieldLengths.CTime);

            _offsetBytes = new byte[FieldLengths.Offset];
            TarHelpers.ReadOrThrow(archiveStream, ref _offsetBytes, FieldLengths.Offset);

            _longNameBytes = new byte[FieldLengths.LongNames];
            TarHelpers.ReadOrThrow(archiveStream, ref _longNameBytes, FieldLengths.LongNames);

            _unusedByte = new byte[FieldLengths.Unused];
            TarHelpers.ReadOrThrow(archiveStream, ref _unusedByte, FieldLengths.Unused);

            _sparseBytes = new byte[FieldLengths.Sparse];
            TarHelpers.ReadOrThrow(archiveStream, ref _sparseBytes, FieldLengths.Sparse);

            _isExtendedByte = new byte[FieldLengths.IsExtended];
            TarHelpers.ReadOrThrow(archiveStream, ref _isExtendedByte, FieldLengths.IsExtended);

            _realSizeBytes = new byte[FieldLengths.RealSize];
            TarHelpers.ReadOrThrow(archiveStream, ref _realSizeBytes, FieldLengths.RealSize);
        }

        // Reads and stores bytes of the POSIX prefix attribute.
        // Throws if end of stream is reached.
        internal void ReadPosixPrefixAttributeBytes(Stream archiveStream)
        {
            _prefixBytes = new byte[FieldLengths.Prefix];
            TarHelpers.ReadOrThrow(archiveStream, ref _prefixBytes, FieldLengths.Prefix);
        }

        // Reads and stores bytes of the V7 padding.
        // Throws if end of stream is reached.
        internal void ReadV7PaddingBytes(Stream archiveStream)
        {
            // Because we tried to detect the magic in case the header was ustar or above,
            // We already advanced those bytes, so we need to substract them from the expected
            // V7 padding length.
            ReadPaddingBytes(archiveStream, FieldLengths.V7Padding - FieldLengths.Magic);
        }

        // Reads and stores bytes of a POSIX padding.
        // Throws if end of stream is reached.
        internal void ReadPosixPaddingBytes(Stream archiveStream) =>
            ReadPaddingBytes(archiveStream, FieldLengths.PosixPadding);

        // Reads and stores bytes of the GNU padding.
        // Throws if end of stream is reached.
        internal void ReadGnuPaddingBytes(Stream archiveStream) =>
            ReadPaddingBytes(archiveStream, FieldLengths.GnuPadding);

        // Reads and stores bytes of a padding field of the specified length.
        // Throws if end of stream is reached.
        private void ReadPaddingBytes(Stream archiveStream, ushort length)
        {
            byte[] padding = new byte[length];
            TarHelpers.ReadOrThrow(archiveStream, ref padding, length);
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
            internal const ushort Sparse = 4 * (12 + 12);
            internal const ushort IsExtended = 1;
            internal const ushort RealSize = 12;

            // Padding lengths depending on format

            internal const ushort V7Padding = 255;
            internal const ushort PosixPadding = 12;
            internal const ushort GnuPadding = 17;
        }
    }
}
