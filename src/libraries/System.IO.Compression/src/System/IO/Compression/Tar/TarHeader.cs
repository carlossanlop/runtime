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

        private const string UstarMagic = "ustar\0";
        private const string UstarVersion = "00";
        private const string GnuMagic = "ustar ";
        private const string GnuVersion = " \0";

        internal const TarArchiveEntryType ExtendedAttributesEntryType = (TarArchiveEntryType)'x';
        internal TarFormat Format { get; set; }
        internal long DataStartPosition { get; private set; }

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

        internal static bool TryGetNextHeader(Stream archiveStream, long lastDataStartPosition, TarFormat currentArchiveFormat, out TarHeader header)
        {
            header = default;

            // If archive format is unknown, this is the first entry we read
            // If pax, then any entries read as ustar should be considered pax
            // Any other combination means the archive is malformed: having
            // multiple incompatible entries in the same archive is not expected.
            header.Format = currentArchiveFormat;

            if (!header.TryReadAttributes(archiveStream, lastDataStartPosition))
            {
                return false;
            }

            if (header.Format == TarFormat.Pax)
            {
                // If the current header type represents extended attributes, then the actual header we
                // need to return is the next one, but with its normal attributes replaced with the ones
                // found in the current header's extended attributes.
                if (header.TypeFlag == ExtendedAttributesEntryType)
                {
                    TarHeader nextHeader = default;
                    nextHeader.Format = TarFormat.Pax;
                    if (!nextHeader.TryReadAttributes(archiveStream, header.DataStartPosition))
                    {
                        return false;
                    }
                    nextHeader.ReplaceNormalAttributesWithExtended(header);
                    header = nextHeader;
                }
            }
            else if (header.Format == TarFormat.Gnu)
            {
                if (header.TypeFlag is
                    TarArchiveEntryType.DirectoryEntry or TarArchiveEntryType.LongLink or TarArchiveEntryType.LongPath)
                {
                    TarHeader nextHeader = default;
                    nextHeader.Format = TarFormat.Gnu;
                    if (!nextHeader.TryReadAttributes(archiveStream, header.DataStartPosition))
                    {
                        return false;
                    }

                    // Nothing to replace from a DirectoryEntry entry
                    if (header.TypeFlag is TarArchiveEntryType.LongLink or TarArchiveEntryType.LongPath)
                    {
                        nextHeader.ReplaceNormalAttributesWithGnuPrefixEntry(header);
                    }
                    header = nextHeader;
                }
            }

            return true;
        }

        private bool TryReadAttributes(Stream archiveStream, long lastDataStartPosition)
        {
            _rawHeader = default;
            // Confirms if v7 or pax, or tentatively selects ustar
            if (!TryReadCommonAttributes(archiveStream))
            {
                return false;
            }

            // Confirms if gnu, or tentatively selects ustar
            if (!TryReadMagicAttribute(archiveStream))
            {
                return false;
            }

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
                // Confirms if gnu
                if (!TryReadVersionAttribute(archiveStream))
                {
                    return false;
                }

                // Fields that ustar, pax and gnu share identically
                if (!TryReadPosixAndGnuSharedAttributes(archiveStream))
                {
                    return false;
                }

                if (Format == TarFormat.Pax)
                {
                    // Pax does not use the prefix for extended paths like ustar.
                    // Long paths are saved in the extended attributes section.
                    if (!_rawHeader.TryReadPosixPrefixAttributeBytes(archiveStream))
                    {
                        return false;
                    }

                    // The padding is the space between end of header and start of:
                    // - The actual extended attributes values if that's the current entry's type.
                    // - The file data, if the previous entry was an extended attributes entry.
                    if (!_rawHeader.TryReadPosixPaddingBytes(archiveStream))
                    {
                        return false;
                    }
                }
                else if (Format == TarFormat.Gnu)
                {
                    if (!TryReadGnuAttributes(archiveStream))
                    {
                        return false;
                    }
                    // The padding is the space between end of the header and the start of the data.
                    if (!_rawHeader.TryReadGnuPaddingBytes(archiveStream))
                    {
                        return false;
                    }
                }
                else if (Format == TarFormat.Ustar)
                {
                    //  - First part of the pathname. If pathname is too long to fit in the 100 bytes of 'name',
                    //    then it can be split by any  '/' characters, with the first portion being stored here.
                    //    So, if prefix is not empty, to obtain the regular pathname, join: 'prefix' + '/' + 'name'.
                    //  - Null terminated unless the entire field is set.
                    if (!TryReadUstarPrefixAttribute(archiveStream))
                    {
                        return false;
                    }
                    // The padding is the space between end of the header and the start of the data.
                    if (!_rawHeader.TryReadPosixPaddingBytes(archiveStream))
                    {
                        return false;
                    }
                }
                else
                {
                    throw new NotSupportedException("Unrecognized tar format.");
                }
            }

            // Read the data or extended attributes section
            if (Format is TarFormat.V7 or TarFormat.Ustar)
            {
                if (!TrySkipFileDataBlock(archiveStream))
                {
                    return false;
                }
            }
            else if (Format == TarFormat.Pax)
            {
                if (TypeFlag is ExtendedAttributesEntryType)
                {
                    if (!TryReadPaxExtendedAttributes(archiveStream))
                    {
                        return false;
                    }
                }
                else
                {
                    if (!TrySkipFileDataBlock(archiveStream))
                    {
                        return false;
                    }
                }
            }
            else if (Format == TarFormat.Gnu)
            {
                if (TypeFlag is TarArchiveEntryType.LongLink or TarArchiveEntryType.LongPath)
                {
                    if (!TryReadGnuLongPathDataBlock(archiveStream))
                    {
                        return false;
                    }
                }
                else
                {
                    if (!TrySkipFileDataBlock(archiveStream))
                    {
                        return false;
                    }
                }
            }
            else
            {
                throw new NotSupportedException("Unsupported format.");
            }

            if (!TrySkipBlockAlignmentPadding(archiveStream, out long paddingAfterData))
            {
                return false;
            }

            DataStartPosition = lastDataStartPosition +
                TarArchive.RecordSize + // normal attributes
                Size +                  // either data or extended attributes
                paddingAfterData;       // block alignment space

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
            // gnu: defines the same as v7, ustar and pax, plus:
            //      K: Long link with the full path in the data section.
            //      L: Long path with the full path in the data section.
            //      D: Directory but with a list of filesystem entries in the data section.
            TypeFlag = (TarArchiveEntryType)_rawHeader._typeFlagBytes[0];

            // If the file is a link, contains the name of the target.
            // v7:
            //  - Null terminated.
            // ustar:
            //  - Null terminated unless the entire field is filled.
            LinkName = GetTrimmedUtf8String(_rawHeader._linkNameBytes);

            if (Format == TarFormat.Unknown)
            {
                if (TypeFlag == ExtendedAttributesEntryType)
                {
                    Format = TarFormat.Pax;
                }
                else if (TypeFlag is
                    TarArchiveEntryType.DirectoryEntry or TarArchiveEntryType.LongLink or TarArchiveEntryType.LongPath)
                {
                    Format = TarFormat.Gnu;
                }
                else
                {
                    // We can quickly determine the minimum possible format if the entry type is the POSIX 'Normal',
                    // because V7 is the only one that uses 'OldNormal'.
                    Format = (TypeFlag == TarArchiveEntryType.Normal) ? TarFormat.Ustar : TarFormat.V7;
                }
            }

            return true;
        }

        // Field only found in ustar or above
        private bool TryReadMagicAttribute(Stream archiveStream)
        {
            if (!_rawHeader.TryReadMagicBytes(archiveStream))
            {
                return false;
            }

            // If at this point the magic value is all nulls, we definitely have a V7
            if (IsAllZeros(_rawHeader._magicBytes))
            {
                Format = TarFormat.V7;
                return true;
            }

            // When the magic field is set, the archive is newer than v7.
            // ustar:
            // - Contains the ASCII  value 'ustar\0'. 6 bytes long.
            // oldgnu and gnu:
            // - Contains the ASCII magic value 'ustar  \0'. 8 bytes long.
            // - As a consequence, it does not have a '00' version string afterwards.
            Magic = GetTrimmedAsciiString(_rawHeader._magicBytes, trim: false);

            if (Magic == GnuMagic)
            {
                Format = TarFormat.Gnu;
            }
            else if (Format == TarFormat.V7 && Magic == UstarMagic)
            {
                // If we could not yet determine a newer format than V7 when reading the
                // TypeFlag common attribute, do it here.
                Format = TarFormat.Ustar;
            }

            return true;
        }

        private bool TryReadVersionAttribute(Stream archiveStream)
        {
            if (Format != TarFormat.V7)
            {
                if (!_rawHeader.TryReadVersionBytes(archiveStream))
                {
                    return false;
                }

                Version = GetTrimmedAsciiString(_rawHeader._versionBytes, trim: false);

                // POSIX have a 6B Magic "ustar\0" and a 2B version "00"
                if ((Format is TarFormat.Ustar or TarFormat.Pax) && Version != UstarVersion)
                {
                    return false;
                }
                // GNU has an 8B magic value of "ustar  \0", and we already read the first 6 bytes in magic
                if (Format == TarFormat.Gnu && Version != GnuVersion)
                {
                    return false;
                }
            }
            return true;
        }

        private bool TryReadPosixAndGnuSharedAttributes(Stream archiveStream)
        {
            if (!_rawHeader.TryReadPosixAndGnuSharedAttributeBytes(archiveStream))
            {
                return false;
            }

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

            return true;
        }

        private bool TryReadGnuAttributes(Stream archiveStream) => _rawHeader.TryReadGnuAttributeBytes(archiveStream);

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

        private bool TryReadPaxExtendedAttributes(Stream archiveStream)
        {
            int totalBytesRead = 0;

            if (ExtendedAttributes == null)
            {
                ExtendedAttributes = new();
            }

            if (!TryReadBytes(archiveStream, Size, out List<byte> byteList))
            {
                return false;
            }
            if (byteList != null && byteList.Count > 0)
            {
                // The PAX attributes data section are saved with UTF8 encoding
                using StringReader reader = new(Encoding.UTF8.GetString(byteList.ToArray()));

                while (totalBytesRead < Size)
                {
                    if (!TryAddNextExtendedAttribute(reader, out int bytesRead))
                    {
                        break;
                    }
                    totalBytesRead += bytesRead;
                }

                if (totalBytesRead != Size)
                {
                    // The reported size for the extended attributes section was incorrect.
                    return false;
                }
            }

            return true;
        }

        private bool TryReadGnuLongPathDataBlock(Stream archiveStream)
        {
            Debug.Assert(TypeFlag is TarArchiveEntryType.LongLink or TarArchiveEntryType.LongPath);

            if (!TryReadBytes(archiveStream, Size, out List<byte> byteList))
            {
                return false;
            }
            if (byteList.Count > 0)
            {
                string longPath = GetTrimmedUtf8String(byteList.ToArray());

                if (TypeFlag == TarArchiveEntryType.LongLink)
                {
                    LinkName = longPath;
                }
                else if (TypeFlag == TarArchiveEntryType.LongPath)
                {
                    Name = longPath;
                }
            }
            return true;
        }

        private bool TryAddNextExtendedAttribute(StringReader reader, out int bytesRead)
        {
            Debug.Assert(ExtendedAttributes != null);

            bytesRead = 0;

            string? nextAttribute = reader.ReadLine();

            if (string.IsNullOrWhiteSpace(nextAttribute))
            {
                return false;
            }

            StringSplitOptions splitOptions = StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries;

            string[] attributeArray = nextAttribute.Split(' ', 2, splitOptions);
            if (attributeArray.Length != 2)
            {
                return false;
            }

            if (!int.TryParse(attributeArray[0], out bytesRead))
            {
                return false;
            }

            string[] keyAndValueArray = attributeArray[1].Split('=', 2, splitOptions);
            if (keyAndValueArray.Length != 2)
            {
                return false;
            }

            ExtendedAttributes.Add(keyAndValueArray[0], keyAndValueArray[1]);

            return true;
        }

        private void ReplaceNormalAttributesWithExtended(TarHeader extendedAttributesHeader)
        {
            Debug.Assert(extendedAttributesHeader.ExtendedAttributes != null);

            Dictionary<string, string> ea = extendedAttributesHeader.ExtendedAttributes;

            if (ea.ContainsKey("uname"))
            {
                UName = ea["uname"];
            }
            if (ea.ContainsKey("uid"))
            {
                Uid = int.Parse(ea["uid"]);
            }
            if (ea.ContainsKey("gname"))
            {
                GName = ea["gname"];
            }
            if (ea.ContainsKey("gid"))
            {
                Gid = int.Parse(ea["gid"]);
            }
            if (ea.ContainsKey("path"))
            {
                Name = ea["path"];
            }
            if (ea.ContainsKey("linkpath"))
            {
                LinkName = ea["linkpath"];
            }
            if (ea.ContainsKey("size"))
            {
                Size = long.Parse(ea["size"]);
            }
        }

        private void ReplaceNormalAttributesWithGnuPrefixEntry(TarHeader previousHeader)
        {
            Debug.Assert(previousHeader.TypeFlag is TarArchiveEntryType.LongLink or TarArchiveEntryType.LongPath);

            if (previousHeader.TypeFlag == TarArchiveEntryType.LongLink)
            {
                LinkName = previousHeader.LinkName;
            }
            else if (previousHeader.TypeFlag == TarArchiveEntryType.LongPath)
            {
                Name = previousHeader.Name;
            }
        }

        // Returns the ASCII string contained in the specified buffer of bytes,
        // removing the trailing null or space chars.
        private string GetTrimmedAsciiString(ReadOnlySpan<byte> buffer, bool trim = true) => GetTrimmedString(buffer, Encoding.ASCII, trim);

        // Returns the UTF8 string contained in the specified buffer of bytes,
        // removing the trailing null or space chars.
        private string GetTrimmedUtf8String(ReadOnlySpan<byte> buffer, bool trim = true) => GetTrimmedString(buffer, Encoding.UTF8, trim);

        // Returns the string contained in the specified buffer of bytes,
        // in the specified encoding, removing the trailing null or space chars.
        private string GetTrimmedString(ReadOnlySpan<byte> buffer, Encoding encoding, bool trim = true)
        {
            int trimmedLength = buffer.Length;
            while (trim && trimmedLength > 0 && IsByteNullOrSpace(buffer[trimmedLength - 1]))
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
        private bool TrySkipBlockAlignmentPadding(Stream archiveStream, out long totalSkipped)
        {
            long ceilingMultipleOfRecordSize = ((TarArchive.RecordSize - 1) | (Size - 1)) + 1;
            int bufferLength = (int)(ceilingMultipleOfRecordSize - Size);
            totalSkipped = bufferLength > 0 ? archiveStream.Read(new byte[bufferLength]) : 0;
            return totalSkipped == bufferLength;
        }

        // Move the stream position to the first byte after the data ends.
        // TODO: This method should go away after figuring out what to do with the data on unseekable streams.
        private bool TrySkipFileDataBlock(Stream archiveStream) => TryReadBytes(archiveStream, Size, out _);

        // TODO: Don't like the list, need to optimize that.
        private bool TryReadBytes(Stream archiveStream, long bytesToRead, out List<byte> byteList)
        {
            byteList = new();
            while (bytesToRead > 0)
            {
                if (bytesToRead > int.MaxValue)
                {
                    byte[] buffer = new byte[int.MaxValue];
                    if (archiveStream.Read(buffer) != int.MaxValue)
                    {
                        return false; // Reached end of stream
                    }
                    byteList.AddRange(buffer);
                    bytesToRead -= int.MaxValue;
                }
                else
                {
                    byte[] buffer = new byte[bytesToRead];
                    archiveStream.Read(buffer);
                    byteList.AddRange(buffer);
                    break;
                }
            }
            return true;
        }

        internal static bool IsAllZeros(byte[] array)
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
    }
}
