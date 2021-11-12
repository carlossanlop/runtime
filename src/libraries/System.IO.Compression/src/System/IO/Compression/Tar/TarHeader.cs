// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using System.Diagnostics;
using System.Text;

namespace System.IO.Compression
{
    internal struct TarHeader
    {
        private RawTarHeader _rawHeader;

        internal TarFormat Format { get; private set; }

        internal string Name { get; private set; }
        internal int Mode { get; private set; }
        internal int Uid { get; private set; }
        internal int Gid { get; private set; }
        internal long Size { get; private set; }
        internal DateTime MTime { get; private set; }
        internal int Checksum { get; private set; }
        internal TarArchiveEntryType TypeFlag { get; private set; }
        internal string LinkName { get; private set; }

        internal long PaddingAfterData { get; private set; }
        internal long DataStartPosition { get; private set; }

        internal static TarHeader GetNextHeader(BinaryReader reader)
        {
            TarHeader header = default;
            header.ReadAttributes(reader);
            return header;
        }

        private void ReadAttributes(BinaryReader reader)
        {
            _rawHeader = default;
            ReadCommonAttributes(reader);
            PaddingAfterData = SkipFileDataBlock(reader, Size);
            DataStartPosition = reader.BaseStream.Position + 1;
        }

        // Fields shared by all tar formats
        private void ReadCommonAttributes(BinaryReader reader)
        {
            _rawHeader.ReadCommonAttributeBytes(reader);

            // The filesystem entry path.
            // v7:
            //  - Expects trailing separator to indicate it's a directory. Null terminated.
            Name = GetTrimmedAsciiString(_rawHeader._nameBytes.AsSpan());

            // File mode, as an octal number in Encoding.ASCII.
            // v7:
            //  - Expects this to be space+null terminated.
            Mode = GetTenBaseNumberFromOctalAsciiChars(_rawHeader._modeBytes);

            // Owner user ID, as an octal number in Encoding.ASCII.
            // - v7 expects this to be space+null terminated.
            Uid = GetTenBaseNumberFromOctalAsciiChars(_rawHeader._uidBytes);

            // Owner group ID, as an octal number in Encoding.ASCII.
            // - v7 expects this to be space+null terminated.
            Gid = GetTenBaseNumberFromOctalAsciiChars(_rawHeader._gidBytes);

            // Size of file, as an octal number in Encoding.ASCII.
            // v7:
            // - Expects this field to be space terminated.
            // - Can be ignored for hardlinks.
            Size = GetTenBaseNumberFromOctalAsciiChars(_rawHeader._sizeBytes);

            // Last modification timestamp, as an octal number in Encoding.ASCII. Represents seconds since the epoch.
            // - v7 expects this to be space terminated.
            int mtime = GetTenBaseNumberFromOctalAsciiChars(_rawHeader._mTimeBytes);
            MTime = DateTimeFromSecondsSinceEpoch(mtime);

            // Header checksum, as an octal number in Encoding.ASCII. Consists of the sum of all
            // the header bytes using unsigned arithmetic.
            // - v7 expects this to be null+space terminated.
            Checksum = GetTenBaseNumberFromOctalAsciiChars(_rawHeader._checksumBytes);

            // The filesystem entry type. v7 calls this field 'linkflag'.
            // - v7 defines:
            //     \0: Normal
            //      1: Hardlink
            //      2: Symlink
            //      3: Character
            //      4: Block
            //      5: Directory
            //      6: Fifo
            //      7: Contiguous
            TypeFlag = (TarArchiveEntryType)_rawHeader._typeFlagBytes;

            // If the file is a link, contains the name of the target.
            // - v7: Null terminated.
            LinkName = GetTrimmedAsciiString(_rawHeader._linkNameBytes.AsSpan());

            // Minimum format expected after all the common attributes were successfully found
            Format = TarFormat.V7;
        }

        // Returns the ASCII string contained in the specified buffer of bytes,
        // removing the trailing null or space chars.
        private string GetTrimmedAsciiString(ReadOnlySpan<byte> buffer)
        {
            int trimmedLength = buffer.Length;
            while (trimmedLength > 0 && IsByteNullOrSpace(buffer[trimmedLength - 1]))
            {
                trimmedLength--;
            }

            return trimmedLength == 0 ? string.Empty : Encoding.ASCII.GetString(buffer.Slice(0, trimmedLength));

            static bool IsByteNullOrSpace(byte c) => c is 0 or 32;
        }

        // Receives a byte array that represents an ASCII string containing a number in octal base.
        // Converts the byte array to an octal base number, then transforms it to decimal base,
        // and returns that value.
        private int GetTenBaseNumberFromOctalAsciiChars(byte[] buffer)
        {
            string str = GetTrimmedAsciiString(buffer.AsSpan());
            return string.IsNullOrEmpty(str) ? 0 : Convert.ToInt32(str, fromBase: 8);
        }

        // Returns a DateTime instance representing the number of seconds that have passed since the Unix Epoch.
        private DateTime DateTimeFromSecondsSinceEpoch(int secondsSinceUnixEpoch)
        {
            DateTimeOffset offset = DateTimeOffset.FromUnixTimeSeconds(secondsSinceUnixEpoch);
            return offset.DateTime;
        }

        // Move the BinaryReader pointer to the first byte of the next file header.
        // Returns the total number of null characters found after the file contents.
        private long SkipFileDataBlock(BinaryReader reader, long bytesToSkip)
        {
            while (bytesToSkip > 0)
            {
                if (bytesToSkip > int.MaxValue)
                {
                    reader.ReadBytes(int.MaxValue);
                    bytesToSkip -= int.MaxValue;
                }
                else
                {
                    reader.ReadBytes((int)bytesToSkip);
                    break;
                }
            }

            // After the file contents, there may be zero or more null characters,
            // which exist to ensure the data is aligned to the record size.
            long afterDataPaddingLength = 0;
            while (reader.PeekChar() == 0)
            {
                reader.ReadByte();
                afterDataPaddingLength++;
            }

            return afterDataPaddingLength;
        }
    }
}
