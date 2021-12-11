// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
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
        internal string? UName { get; private set; }
        internal string? GName { get; private set; }
        internal int DevMajor { get; private set; }
        internal int DevMinor { get; private set; }

        // POSIX attributes

        internal string Prefix { get; private set; }

        // PAX extended attributes
        internal Dictionary<string, string>? ExtendedAttributes;

        internal long DataStartPosition { get; private set; }

        internal static bool TryGetNextHeader(Stream archiveStream, long lastDataStartPosition, out TarHeader header)
        {
            header = default;
            header.Format = TarFormat.Unknown;
            return header.TryReadAttributes(archiveStream, lastDataStartPosition);
        }

        private bool TryReadAttributes(Stream archiveStream, long lastDataStartPosition)
        {
            _rawHeader = default;
            if (!TryReadCommonAttributes(archiveStream))
            {
                return false;
            }

            ReadMagicAttribute(archiveStream);

            if (Format == TarFormat.V7)
            {
                // Space between end of header and start of file data.
                if (!_rawHeader.TryReadV7PaddingBytes(archiveStream))
                {
                    return false;
                }
            }
            else
            {
                // Fields that ustar, pax and gnu share identically
                if (!TryReadPosixAndGnuSharedAttributes(archiveStream))
                {
                    return false;
                }

                if (Format == TarFormat.Ustar)
                {
                    // ustar:
                    //  - First part of the pathname. If pathname is too long to fit in the 100 bytes of 'name',
                    //      then it can be split by any  '/' characters, with the first portion being stored here.
                    //      So, if prefix is not empty, to obtain the regular pathname, join: 'prefix' + '/' + 'name'.
                    //  - Null terminated unless the entire field is set.
                    if (!TryReadUstarPrefixAttribute(archiveStream))
                    {
                        return false;
                    }
                    // ustar: Padding is the space between end of header and start of file data.
                    if (!_rawHeader.TryReadPosixPaddingBytes(archiveStream))
                    {
                        return false;
                    }
                }
                else if (Format == TarFormat.Pax)
                {
                    // pax: Does not use the prefix for extended paths like ustar.
                    // Long paths are saved in the extended attributes section.
                    if (!_rawHeader.TryReadPosixPrefixAttributeBytes(archiveStream))
                    {
                        return false;
                    }

                    // pax: Padding is the space between end of header and start of:
                    // - The extended attributes, if the TypeFlag is x or g.
                    // - The file data, if the previous entry had a TypeFlag of x, or the first entry was g.
                    if (!_rawHeader.TryReadPosixPaddingBytes(archiveStream))
                    {
                        return false;
                    }
                }
                else
                {
                    throw new NotImplementedException("gnu format not yet implemented");
                }
            }

            // Read the data or extended attributes section
            long paddingAfterData;
            if (Format is TarFormat.V7 or TarFormat.Ustar)
            {
                SkipFileDataBlock(archiveStream);
                paddingAfterData = SkipBlockAlignmentPadding(archiveStream);
            }
            else if (Format == TarFormat.Pax)
            {
                if (TypeFlag is TarArchiveEntryType.ExtendedAttributes or TarArchiveEntryType.GlobalExtendedAttributes)
                {
                    ExtendedAttributes = ReadPaxExtendedAttributes(archiveStream);
                    paddingAfterData = SkipBlockAlignmentPadding(archiveStream);
                }
                else
                {
                    SkipFileDataBlock(archiveStream);
                    paddingAfterData = SkipBlockAlignmentPadding(archiveStream);
                }
            }
            else
            {
                throw new NotImplementedException("gnu format not yet implemented");
            }

            DataStartPosition = lastDataStartPosition + 512 + Size + paddingAfterData;

            return true;
        }

        // Fields shared by all tar formats
        private bool TryReadCommonAttributes(Stream archiveStream)
        {
            if (!_rawHeader.TryReadCommonAttributeBytes(archiveStream))
            {
                return false;
            }

            // The filesystem entry path.
            // v7:
            //  - Expects trailing separator to indicate it's a directory. Null terminated.
            // ustar:
            //  - Does not expect trailing separator for directory (that's what typeflag is for), but should add it for backwards-compat.
            //  - Null terminated unless the entire field is filled.
            Name = GetTrimmedUtf8String(_rawHeader._nameBytes);

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

            // Zero checksum means this is a null block
            if (Checksum == 0)
            {
                return false;
            }

            // The filesystem entry type. v7 calls this field 'linkflag'.
            // v7 defines:
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
            // pax defines the same as v7 and ustar, plus:
            //      x: Entry with extended attributes to describe the next entry.
            //      g: Entry with global extended attributes to describe all the rest of the entries.
            //  other: Unrecognized values that are treated as Normal.
            TypeFlag = (TarArchiveEntryType)_rawHeader._typeFlagBytes[0];

            // If the file is a link, contains the name of the target.
            // v7:
            //  - Null terminated.
            // ustar:
            //  - Null terminated unless the entire field is filled.
            LinkName = GetTrimmedUtf8String(_rawHeader._linkNameBytes);

            if (TypeFlag is
                TarArchiveEntryType.ExtendedAttributes or TarArchiveEntryType.GlobalExtendedAttributes)
            {
                Format = TarFormat.Pax;
            }
            else
            {
                // We can quickly determine the minimum possible format if the entry type is the POSIX 'Normal'.
                Format = (TypeFlag == TarArchiveEntryType.Normal) ? TarFormat.Ustar : TarFormat.V7;
            }

            return true;
        }

        // Field only found in ustar or above
        private void ReadMagicAttribute(Stream archiveStream)
        {
            if (!_rawHeader.TryReadMagicBytes(archiveStream))
            {
                return;
            }

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

        private bool TryReadPosixAndGnuSharedAttributes(Stream archiveStream)
        {
            if (!_rawHeader.TryReadPosixAndGnuSharedAttributeBytes(archiveStream))
            {
                return false;
            }

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
            if (TypeFlag is TarArchiveEntryType.Character or TarArchiveEntryType.Block)
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
                return false;
            }

            return true;
        }

        private bool TryReadUstarPrefixAttribute(Stream archiveStream)
        {
            if (!_rawHeader.TryReadPosixPrefixAttributeBytes(archiveStream))
            {
                return false;
            }

            Prefix = GetTrimmedUtf8String(_rawHeader._prefixBytes);

            // The Prefix byte array is used to store the ending path segments that did not fit in the Name byte array.
            // Note: Prefix may end in a directory separator.
            if (!string.IsNullOrEmpty(Prefix))
            {
                Name = Path.Join(Prefix, Name);
            }

            return true;
        }

        private Dictionary<string, string>? ReadPaxExtendedAttributes(Stream archiveStream)
        {
            Dictionary<string, string> attributes = new();
            int totalBytesRead = 0;
            StreamReader reader = new(archiveStream, Encoding.UTF8);

            while (totalBytesRead < Size)
            {
                if (!TryGetNextExtendedAttribute(attributes, reader, out int bytesRead))
                {
                    break;
                }
                totalBytesRead += bytesRead;
            }

            if (totalBytesRead != Size)
            {
                throw new FormatException("The reported size for the extended attributes section was incorrect."); // TODO
            }

            return attributes;
        }

        private bool TryGetNextExtendedAttribute(Dictionary<string, string> attributes, StreamReader reader, out int bytesRead)
        {
            bytesRead = 0;
            string? nextAttribute = reader.ReadLine();

            if (nextAttribute == null)
            {
                return false;
            }

            if (string.IsNullOrWhiteSpace(nextAttribute))
            {
                return false;
            }

            string[] attributeArray = nextAttribute.Split(' ', 2, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            if (attributeArray.Length != 2)
            {
                return false;
            }

            if (!int.TryParse(attributeArray[0], out bytesRead))
            {
                return false;
            }

            string[] keyAndValueArray = attributeArray[1].Split('=', 2, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            if (keyAndValueArray.Length != 2)
            {
                return false;
            }

            attributes.Add(keyAndValueArray[0], keyAndValueArray[1]);

            return true;
        }

        // Returns the ASCII string contained in the specified buffer of bytes,
        // removing the trailing null or space chars.
        private string GetTrimmedAsciiString(ReadOnlySpan<byte> buffer) => GetTrimmedString(buffer, Encoding.ASCII);

        // Returns the UTF8 string contained in the specified buffer of bytes,
        // removing the trailing null or space chars.
        private string GetTrimmedUtf8String(ReadOnlySpan<byte> buffer) => GetTrimmedString(buffer, Encoding.UTF8);

        // Returns the string contained in the specified buffer of bytes,
        // in the specified encoding, removing the trailing null or space chars.
        private string GetTrimmedString(ReadOnlySpan<byte> buffer, Encoding encoding)
        {
            int trimmedLength = buffer.Length;
            while (trimmedLength > 0 && IsByteNullOrSpace(buffer[trimmedLength - 1]))
            {
                trimmedLength--;
            }

            return trimmedLength == 0 ? string.Empty : encoding.GetString(buffer.Slice(0, trimmedLength));

            static bool IsByteNullOrSpace(byte c) => c is 0 or 32;
        }

        // Receives a byte array that represents an ASCII string containing a number in octal base.
        // Converts the byte array to an octal base number, then transforms it to decimal base,
        // and returns that value.
        private int GetTenBaseNumberFromOctalAsciiChars(Span<byte> buffer)
        {
            string str = GetTrimmedAsciiString(buffer);
            return string.IsNullOrEmpty(str) ? 0 : Convert.ToInt32(str, fromBase: 8);
        }

        // Returns a DateTime instance representing the number of seconds that have passed since the Unix Epoch.
        private DateTime DateTimeFromSecondsSinceEpoch(int secondsSinceUnixEpoch)
        {
            DateTimeOffset offset = DateTimeOffset.FromUnixTimeSeconds(secondsSinceUnixEpoch);
            return offset.DateTime;
        }


        // After the file contents, there may be zero or more null characters,
        // which exist to ensure the data is aligned to the record size. Skip them and
        // set the stream position to the first byte of the next entry.
        private long SkipBlockAlignmentPadding(Stream archiveStream)
        {
            long ceilingMultipleOfRecordSize = ((TarArchive.RecordSize - 1) | (Size - 1)) + 1;
            int bufferLength = (int)(ceilingMultipleOfRecordSize - Size);
            if (bufferLength > 0)
            {
                return archiveStream.Read(new byte[bufferLength]);
            }
            return 0;
        }

        // Move the stream position to the first byte after the data ends.
        // TODO: This method should go away after figuring out what to do with the data on unseekable streams.
        private void SkipFileDataBlock(Stream archiveStream)
        {
            long bytesToSkip = Size;
            byte[]? buffer = null;
            while (bytesToSkip > 0)
            {
                if (bytesToSkip > int.MaxValue)
                {
                    if (buffer == null)
                    {
                        buffer = new byte[int.MaxValue];
                    }
                    if (archiveStream.Read(buffer) != int.MaxValue)
                    {
                        // Reached end of stream
                        return;
                    }
                    bytesToSkip -= int.MaxValue;
                }
                else
                {
                    archiveStream.Read(new byte[bytesToSkip]);
                    return;
                }
            }
        }
    }
}
