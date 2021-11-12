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

        // Common attributes

        internal string Name { get; private set; }
        internal int Mode { get; private set; }
        internal int Uid { get; private set; }
        internal int Gid { get; private set; }
        internal long Size { get; private set; }
        internal DateTime MTime { get; private set; }
        internal int Checksum { get; private set; }
        internal TarArchiveEntryType TypeFlag { get; private set; }
        internal string LinkName { get; private set; }

        // POSIX and GNU shared attributes

        internal string Magic { get; private set; }
        internal string Version { get; private set; }
        internal string UName { get; private set; }
        internal string GName { get; private set; }
        internal int DevMajor { get; private set; }
        internal int DevMinor { get; private set; }

        // POSIX attributes

        internal string Prefix { get; private set; }


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
            ReadMagicAttribute(reader);

            if (Format == TarFormat.V7)
            {
                // Space between end of header and start of file data.
                _rawHeader.ReadV7PaddingBytes(reader);
            }
            else
            {
                ReadPosixAndGnuSharedAttributes(reader);

                if (Format == TarFormat.Ustar)
                {
                    // ustar:
                    //  - First part of the pathname. If pathname is too long to fit in the 100 bytes of 'name',
                    //      then it can be split by any  '/' characters, with the first portion being stored here.
                    //      So, if prefix is not empty, to obtain the regular pathname, join: 'prefix' + '/' + 'name'.
                    //  - Null terminated unless the entire field is set.
                    _rawHeader.ReadPosixPrefixAttributeBytes(reader);

                    // Space between end of header and start of file data.
                    _rawHeader.ReadPosixPaddingBytes(reader);
                    // Now try to determine if it's pax or should stay ustar
                }
            }

            PaddingAfterData = SkipFileDataBlock(reader);
            DataStartPosition = reader.BaseStream.Position + 1;
        }

        // Fields shared by all tar formats
        private void ReadCommonAttributes(BinaryReader reader)
        {
            _rawHeader.ReadCommonAttributeBytes(reader);

            // The filesystem entry path.
            // v7:
            //  - Expects trailing separator to indicate it's a directory. Null terminated.
            // ustar:
            //  - Does not expect trailing separator for directory (that's what typeflag is for), but should add it for backwards-compat.
            //  - Null terminated unless the entire field is filled.
            Name = GetTrimmedAsciiString(_rawHeader._nameBytes.AsSpan());

            // File mode, as an octal number in Encoding.ASCII.
            // v7:
            //  - Expects this to be space+null terminated.
            // ustar:
            //  - Expects this to be zero-padded in the front, and space OR null terminated.
            Mode = GetTenBaseNumberFromOctalAsciiChars(_rawHeader._modeBytes);

            // Owner user ID, as an octal number in Encoding.ASCII.
            // v7
            //  - Expects this to be space+null terminated.
            // ustar:
            //  - Expects this to be zero-padded in the front, and space OR null terminated.
            Uid = GetTenBaseNumberFromOctalAsciiChars(_rawHeader._uidBytes);

            // Owner group ID, as an octal number in Encoding.ASCII.
            // v7:
            //  - Expects this to be space+null terminated.
            // ustar:
            //  - Expects this to be zero-padded in the front, and space OR null terminated.
            Gid = GetTenBaseNumberFromOctalAsciiChars(_rawHeader._gidBytes);

            // Size of file, as an octal number in Encoding.ASCII.
            // v7:
            // - Expects this field to be space terminated.
            // - Can be ignored for hardlinks.
            // ustar:
            // - Expects this field to be zero-padded in the front, and space OR null terminated.
            // - Normal files: indicates the amount of data following the header.
            // - Directories: may indicate the total size of all files in the directory, so
            //   operating systems that preallocate directory space can use it. Usually expected to be zero.
            // - All other types: it should be zero and ignored by readers.
            Size = GetTenBaseNumberFromOctalAsciiChars(_rawHeader._sizeBytes);

            // Last modification timestamp, as an octal number in Encoding.ASCII. Represents seconds since the epoch.
            // v7:
            //  - Expects this to be space terminated.
            // ustar:
            //  - Expects this to be zero-padded in the front, and space OR null terminated.
            int mtime = GetTenBaseNumberFromOctalAsciiChars(_rawHeader._mTimeBytes);
            MTime = DateTimeFromSecondsSinceEpoch(mtime);

            // Header checksum, as an octal number in Encoding.ASCII. Consists of the sum of all
            // the header bytes using unsigned arithmetic.
            // v7:
            //  - Expects this to be null+space terminated.
            // ustar:
            //  - Expects this to be zero-padded in the front, and space OR null terminated.
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
            // ustar defines the same as v7, plus:
            //      0: Normal (ustar version)
            //  other: Unrecognized values that are treated as Normal.
            TypeFlag = (TarArchiveEntryType)_rawHeader._typeFlagBytes;

            // If the file is a link, contains the name of the target.
            // v7:
            //  - Null terminated.
            // ustar:
            //  - Null terminated unless the entire field is filled.
            LinkName = GetTrimmedAsciiString(_rawHeader._linkNameBytes.AsSpan());

            // We can quickly determine the minimum possible format if the entry type is the POSIX 'Normal'.
            Format = (TypeFlag == TarArchiveEntryType.Normal) ? TarFormat.Ustar : TarFormat.V7;
        }

        // Field only found in ustar or above
        private void ReadMagicAttribute(BinaryReader reader)
        {
            _rawHeader.ReadMagicBytes(reader);

            // If the magic field is set, the archive is newer than v7.
            // ustar:
            // - Contains the magic value 'ustar'. Null terminated.
            // - Full ustar compliance require uname and gname to be properly set.
            Magic = GetTrimmedAsciiString(_rawHeader._magicBytes);

            // Determine if the format is at least Ustar, if we could not yet
            // find this out when reading the TypeFlag common attribute.
            if (Format == TarFormat.V7 &&
                // There's a rare 'ustar' variant that has a space at the end,
                // but it's ustar compatible nonetheless
                Magic.Length >= 5 && Magic[0..5] == "ustar")
            {
                // At the very least, it's ustar, but we need to look for
                // more details later, to determine if it's pax or gnu
                Format = TarFormat.Ustar;
            }
        }

        private void ReadPosixAndGnuSharedAttributes(BinaryReader reader)
        {
            _rawHeader.ReadPosixAndGnuSharedAttributeBytes(reader);

            // ASCII string field that helps determine if the format is ustar or newer.
            // ustar:
            // - If magic is set, version is two zero digits: "00". Gnu is " \0"
            Version = GetTrimmedAsciiString(_rawHeader._versionBytes);

            // ASCII user name.
            // ustar:
            //  - Null terminated.
            //  - Used in preference to uid if the user name exists in the system.
            UName = GetTrimmedAsciiString(_rawHeader._uNameBytes);

            // ASCII group name.
            // ustar:
            //  - Null terminated.
            //  - Used in preference to gid if the group name exists in the system.
            GName = GetTrimmedAsciiString(_rawHeader._gNameBytes);

            // These fields only have valid numbers with these two entry types,
            // otherwise they are filled with nulls or spaces
            if (TypeFlag == TarArchiveEntryType.Character || TypeFlag == TarArchiveEntryType.Block)
            {
                // Major number for a character device or block device entry.
                // ustar:
                //  - Expected to be zero-padded in the front, and space OR null terminated.
                DevMajor = GetTenBaseNumberFromOctalAsciiChars(_rawHeader._devMajorBytes);

                // Minor number for a character device or block device entry.
                // ustar:
                // - Expected to be zero-padded in the front, and space OR null terminated.
                DevMinor = GetTenBaseNumberFromOctalAsciiChars(_rawHeader._devMinorBytes);
            }

            if (_rawHeader._versionBytes[0] == 32 && // space
                _rawHeader._versionBytes[1] == 0) // null
            {
                throw new NotImplementedException("GNU");
            }
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
        private long SkipFileDataBlock(BinaryReader reader)
        {
            long bytesToSkip = Size;
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
