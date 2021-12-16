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
        internal byte[] _typeFlagByte;
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

        // Reads and stores bytes of fields found in all formats.
        // Throws if end of stream is reached.
        // Returns true if everything was read successfully.
        // Returns false if the checksum is empty.
        internal bool TryReadCommonAttributeBytes(Stream archiveStream)
        {
            _nameBytes = new byte[FieldLengths.Name];
            ReadOrThrow(archiveStream, ref _nameBytes, FieldLengths.Name);

            _modeBytes = new byte[FieldLengths.Mode];
            ReadOrThrow(archiveStream, ref _modeBytes, FieldLengths.Mode);

            _uidBytes = new byte[FieldLengths.Uid];
            ReadOrThrow(archiveStream, ref _uidBytes, FieldLengths.Uid);

            _gidBytes = new byte[FieldLengths.Gid];
            ReadOrThrow(archiveStream, ref _gidBytes, FieldLengths.Gid);

            _sizeBytes = new byte[FieldLengths.Size];
            ReadOrThrow(archiveStream, ref _sizeBytes, FieldLengths.Size);

            _mTimeBytes = new byte[FieldLengths.MTime];
            ReadOrThrow(archiveStream, ref _mTimeBytes, FieldLengths.MTime);

            _checksumBytes = new byte[FieldLengths.Checksum];
            ReadOrThrow(archiveStream, ref _checksumBytes, FieldLengths.Checksum);

            // Empty checksum means this is an invalid (all blank) entry, finish early
            if (TarHeader.IsAllNullBytes(_checksumBytes))
            {
                return false;
            }

            _typeFlagByte = new byte[FieldLengths.TypeFlag];
            ReadOrThrow(archiveStream, ref _typeFlagByte, FieldLengths.TypeFlag);

            _linkNameBytes = new byte[FieldLengths.LinkName];
            ReadOrThrow(archiveStream, ref _linkNameBytes, FieldLengths.LinkName);

            return true;
        }

        // Reads and stores the bytes of the magic field.
        // Throws if the end of stream is reached.
        internal void ReadMagicBytes(Stream archiveStream)
        {
            _magicBytes = new byte[FieldLengths.Magic];
            ReadOrThrow(archiveStream, ref _magicBytes, FieldLengths.Magic);
        }

        // Reads and stores the bytes of the version field.
        // Throws if the end of stream is reached.
        internal void ReadVersionBytes(Stream archiveStream)
        {
            _versionBytes = new byte[FieldLengths.Version];
            ReadOrThrow(archiveStream, ref _versionBytes, FieldLengths.Version);
        }

        // Reads and stores bytes of fields found in the POSIX and GNU formats.
        // Throws if end of stream is reached.
        internal void ReadPosixAndGnuSharedAttributeBytes(Stream archiveStream)
        {
            _uNameBytes = new byte[FieldLengths.UName];
            ReadOrThrow(archiveStream, ref _uNameBytes, FieldLengths.UName);

            _gNameBytes = new byte[FieldLengths.GName];
            ReadOrThrow(archiveStream, ref _gNameBytes, FieldLengths.GName);

            _devMajorBytes = new byte[FieldLengths.DevMajor];
            ReadOrThrow(archiveStream, ref _devMajorBytes, FieldLengths.DevMajor);

            _devMinorBytes = new byte[FieldLengths.DevMinor];
            ReadOrThrow(archiveStream, ref _devMinorBytes, FieldLengths.DevMinor);
        }

        // Reads and stores bytes of fields found in the GNU format.
        // Throws if end of stream is reached.
        internal void ReadGnuAttributeBytes(Stream archiveStream)
        {
            _atimeBytes = new byte[FieldLengths.ATime];
            ReadOrThrow(archiveStream, ref _atimeBytes, FieldLengths.ATime);

            _ctimeBytes = new byte[FieldLengths.CTime];
            ReadOrThrow(archiveStream, ref _ctimeBytes, FieldLengths.CTime);

            _offsetBytes = new byte[FieldLengths.Offset];
            ReadOrThrow(archiveStream, ref _offsetBytes, FieldLengths.Offset);

            _longNameBytes = new byte[FieldLengths.LongNames];
            ReadOrThrow(archiveStream, ref _longNameBytes, FieldLengths.LongNames);

            _unusedByte = new byte[FieldLengths.Unused];
            ReadOrThrow(archiveStream, ref _unusedByte, FieldLengths.Unused);

            _sparseItems = new Sparse[FieldLengths.SparseItems];
            for (int i = 0; i < FieldLengths.SparseItems; i++)
            {
                _sparseItems[i]._offsetBytes = new byte[FieldLengths.SparseOffset];
                ReadOrThrow(archiveStream, ref _sparseItems[i]._offsetBytes, FieldLengths.SparseOffset);

                _sparseItems[i]._byteNumberBytes = new byte[FieldLengths.SparseByteNumber];
                ReadOrThrow(archiveStream, ref _sparseItems[i]._byteNumberBytes, FieldLengths.SparseByteNumber);
            }

            _isExtendedByte = new byte[FieldLengths.IsExtended];
            ReadOrThrow(archiveStream, ref _isExtendedByte, FieldLengths.IsExtended);

            _realSizeBytes = new byte[FieldLengths.RealSize];
            ReadOrThrow(archiveStream, ref _realSizeBytes, FieldLengths.RealSize);
        }

        // Reads and stores bytes of the POSIX prefix attribute.
        // Throws if end of stream is reached.
        internal void ReadPosixPrefixAttributeBytes(Stream archiveStream)
        {
            _prefixBytes = new byte[FieldLengths.Prefix];
            ReadOrThrow(archiveStream, ref _prefixBytes, FieldLengths.Prefix);
        }

        // Reads and stores bytes of the V7 padding.
        // Throws if end of stream is reached.
        internal void ReadV7PaddingBytes(Stream archiveStream) =>
            // Because we tried to detect the magic in case the header was ustar or above,
            // We already advanced those bytes, so we need to substract them from the expected
            // V7 padding length.
            ReadPaddingBytes(archiveStream, FieldLengths.V7Padding - FieldLengths.Magic);

        // Reads and stores bytes of a POSIX padding.
        // Throws if end of stream is reached.
        internal void ReadPosixPaddingBytes(Stream archiveStream) => ReadPaddingBytes(archiveStream, FieldLengths.PosixPadding);

        // Reads and stores bytes of the GNU padding.
        // Throws if end of stream is reached.
        internal void ReadGnuPaddingBytes(Stream archiveStream) => ReadPaddingBytes(archiveStream, FieldLengths.GnuPadding);

        // Reads and stores bytes of a padding field of the specified length.
        // Throws if end of stream is reached.
        private void ReadPaddingBytes(Stream archiveStream, ushort length)
        {
            _paddingBytes = new byte[length];
            ReadOrThrow(archiveStream, ref _paddingBytes, length);
        }

        // Reads the specified number of bytes and stores it in the byte buffer passed by reference.
        // Throws if end of stream is reached.
        private void ReadOrThrow(Stream archiveStream, ref byte[] buffer, int bytesToRead)
        {
            if (archiveStream.Read(buffer.AsSpan()) != bytesToRead)
            {
                throw new EndOfStreamException();
            }
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
