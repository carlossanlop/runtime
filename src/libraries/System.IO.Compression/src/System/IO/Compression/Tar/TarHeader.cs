// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Buffers;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;

namespace System.IO.Compression.Tar
{
    // Retrieves and stores all the attributes found in a tar archive entry.
    internal struct TarHeader
    {
        private const string UstarMagic = "ustar\0";
        private const string UstarVersion = "00";
        private const string GnuMagic = "ustar ";
        private const string GnuVersion = " \0";

        private TarBlocks _blocks;
        internal Stream? _dataStream;
        internal long _endOfHeader;

        internal TarFormat Format { get; private set; }

        // Common attributes

        internal string Name { get; private set; }
        internal int Mode { get; private set; }
        internal int Uid { get; private set; }
        internal int Gid { get; private set; }
        internal long Size { get; private set; }
        internal DateTime MTime { get; private set; }
        internal int Checksum { get; private set; }
        internal TarEntryTypeFlag TypeFlag { get; private set; }
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

        // PAX attributes

        internal Dictionary<string, string>? _extendedAttributes;

        // GNU attributes

        internal DateTime ATime { get; private set; }
        internal DateTime CTime { get; private set; }


        // Attempts to read the next tar archive entry header.
        // Returns true if a full header was read successfully, false otherwise.
        internal static bool TryGetNextHeader(Stream archiveStream, TarFormat currentArchiveFormat, out TarHeader header)
        {
            header = default;

            // If the archive format is unknown, this is the first entry we read.
            // If the format is pax, then any entries read as ustar should be considered pax.
            // Any other combination means the archive is malformed: having multiple
            // incompatible entries in the same archive is not expected.
            header.Format = currentArchiveFormat;

            if (!header.TryReadAttributes(archiveStream))
            {
                return false;
            }

            if (header.Format == TarFormat.Pax)
            {
                // If the current header type represents extended attributes 'x', then the actual
                // header we need to return is the next one, with its normal attributes replaced
                // with those found in the current entry.
                if (header.TypeFlag == TarEntryTypeFlag.ExtendedAttributes)
                {
                    TarHeader nextHeader = default;
                    nextHeader.Format = TarFormat.Pax;
                    if (!nextHeader.TryReadAttributes(archiveStream))
                    {
                        return false;
                    }
                    nextHeader.ReplaceNormalAttributesWithExtended(header);
                    header = nextHeader;
                }
            }
            else if (header.Format == TarFormat.Gnu)
            {
                if (header.TypeFlag is TarEntryTypeFlag.LongLink or TarEntryTypeFlag.LongPath)
                {
                    // LongLink and LongPath are metadata entries.
                    // They contain a very long path in their data section.
                    // We retrieve the string and replace it in the Name or LinkName field,
                    // then retrieve the next entry, which is the actual entry.

                    TarHeader nextHeader = default;
                    nextHeader.Format = TarFormat.Gnu;
                    if (!nextHeader.TryReadAttributes(archiveStream))
                    {
                        return false;
                    }

                    if (header.TypeFlag is TarEntryTypeFlag.LongLink or TarEntryTypeFlag.LongPath)
                    {
                        nextHeader.ReplaceGnuPaths(header);
                    }
                    header = nextHeader;
                }
            }

            return true;
        }

        // Appends or overwrites the passed global extended attributes into the current
        // header's extended attributes dictionary.
        internal void AppendGlobalExtendedAttributesIfNeeded(Dictionary<string, string> globalExtendedAttributes)
        {
            if (_extendedAttributes == null)
            {
                _extendedAttributes = globalExtendedAttributes;
            }
            else
            {
                foreach ((string key, string value) in globalExtendedAttributes)
                {
                    if (!_extendedAttributes.TryAdd(key, value))
                    {
                        _extendedAttributes[key] = value;
                    }
                }
            }
        }

        // Attempts to read all the fields of the header.
        // Throws if end of stream is reached or if any data type conversion fails.
        // Returns true if all the attributes were read successfully, false otherwise.
        private bool TryReadAttributes(Stream archiveStream)
        {
            _blocks = default;

            // Confirms if v7 or pax, or tentatively selects ustar
            if (!TryReadCommonAttributes(archiveStream))
            {
                return false;
            }

            // Confirms if gnu, or tentatively selects ustar
            ReadMagicAttribute(archiveStream);

            if (Format == TarFormat.V7)
            {
                // Space between end of header and start of file data.
                _blocks.ReadV7PaddingBytes(archiveStream);
            }
            else
            {
                // Confirms if gnu
                ReadVersionAttribute(archiveStream);

                // Fields that ustar, pax and gnu share identically
                ReadPosixAndGnuSharedAttributes(archiveStream);

                Debug.Assert(Format is TarFormat.Ustar or TarFormat.Pax or TarFormat.Gnu);
                if (Format == TarFormat.Ustar)
                {
                    ReadUstarAttributes(archiveStream);
                }
                else if (Format == TarFormat.Pax)
                {
                    ReadPaxAttributes(archiveStream);
                }
                else if (Format == TarFormat.Gnu)
                {
                    ReadGnuAttributes(archiveStream);
                }
            }

            ProcessDataBlock(archiveStream);
            SkipBlockAlignmentPadding(archiveStream);

            return true;
        }

        // Attempts to read the fields shared by all formats and stores them in their expected data type.
        // Throws if end of stream is reached or if any data type conversion fails.
        // Returns true on success, false if checksum is zero.
        private bool TryReadCommonAttributes(Stream archiveStream)
        {
            if (!_blocks.TryReadCommonAttributeBytes(archiveStream))
            {
                return false;
            }

            Name = TarHelpers.GetTrimmedUtf8String(_blocks._nameBytes);
            Mode = TarHelpers.GetTenBaseNumberFromOctalAsciiChars(_blocks._modeBytes);
            Uid = TarHelpers.GetTenBaseNumberFromOctalAsciiChars(_blocks._uidBytes);
            Gid = TarHelpers.GetTenBaseNumberFromOctalAsciiChars(_blocks._gidBytes);
            Size = TarHelpers.GetTenBaseNumberFromOctalAsciiChars(_blocks._sizeBytes);

            int mtime = TarHelpers.GetTenBaseNumberFromOctalAsciiChars(_blocks._mTimeBytes);
            MTime = TarHelpers.DateTimeFromSecondsSinceEpoch(mtime);

            Checksum = TarHelpers.GetTenBaseNumberFromOctalAsciiChars(_blocks._checksumBytes);

            // Zero checksum means the whole header is empty
            if (Checksum == 0)
            {
                return false;
            }

            TypeFlag = (TarEntryTypeFlag)_blocks._typeFlagByte[0];

            if (TypeFlag is TarEntryTypeFlag.MultiVolume or
                            TarEntryTypeFlag.RenamedOrSymlinked or
                            TarEntryTypeFlag.Sparse or
                            TarEntryTypeFlag.TapeVolume)
            {
                throw new NotSupportedException(string.Format(SR.TarEntryTypeNotSupported, TypeFlag));
            }

            LinkName = TarHelpers.GetTrimmedUtf8String(_blocks._linkNameBytes);

            if (Format == TarFormat.Unknown)
            {
                if (TypeFlag is TarEntryTypeFlag.ExtendedAttributes or TarEntryTypeFlag.GlobalExtendedAttributes)
                {
                    Format = TarFormat.Pax;
                }
                else if (TypeFlag is TarEntryTypeFlag.DirectoryEntry or TarEntryTypeFlag.LongLink or TarEntryTypeFlag.LongPath or TarEntryTypeFlag.Contiguous)
                {
                    Format = TarFormat.Gnu;
                }
                else
                {
                    // We can quickly determine the minimum possible format if the entry type is the
                    // POSIX 'Normal', because V7 is the only one that uses 'OldNormal'.
                    Format = (TypeFlag == TarEntryTypeFlag.Normal) ? TarFormat.Ustar : TarFormat.V7;
                }
            }

            return true;
        }

        // Reads fields only found in ustar format or above and converts them to their expected data type.
        // Throws if end of stream is reached or if any conversion fails.
        private void ReadMagicAttribute(Stream archiveStream)
        {
            _blocks.ReadMagicBytes(archiveStream);

            // If at this point the magic value is all nulls, we definitely have a V7
            if (TarHelpers.IsAllNullBytes(_blocks._magicBytes))
            {
                Format = TarFormat.V7;
                return;
            }

            // When the magic field is set, the archive is newer than v7.
            Magic = TarHelpers.GetTrimmedAsciiString(_blocks._magicBytes, trim: false);

            if (Magic == GnuMagic)
            {
                Format = TarFormat.Gnu;
            }
            else if (Format == TarFormat.V7 && Magic == UstarMagic)
            {
                // Important: Only change to ustar if we had not changed the format to pax already
                Format = TarFormat.Ustar;
            }
        }

        // Reads the version string and determines the format depending on its value.
        // Throws if end of stream is reached, if converting the bytes to string fails,
        // or if an unexpected version string is found.
        private void ReadVersionAttribute(Stream archiveStream)
        {
            if (Format != TarFormat.V7)
            {
                _blocks.ReadVersionBytes(archiveStream);

                Version = TarHelpers.GetTrimmedAsciiString(_blocks._versionBytes, trim: false);

                // The POSIX formats have a 6 byte Magic "ustar\0", followed by a 2 byte Version "00"
                if ((Format is TarFormat.Ustar or TarFormat.Pax) && Version != UstarVersion)
                {
                    throw new FormatException(string.Format(SR.TarPosixFormatExpected, Name));
                }
                // The GNU format has a Magic+Version 8 byte string "ustar  \0"
                else if (Format == TarFormat.Gnu && Version != GnuVersion)
                {
                    throw new FormatException(string.Format(SR.TarGnuFormatExpected, Name));
                }
            }
        }

        // Reads the attributes shared by the POSIX and GNU formats.
        // Throws if end of stream is reached or if converting the bytes to their expected data type fails.
        private void ReadPosixAndGnuSharedAttributes(Stream archiveStream)
        {
            _blocks.ReadPosixAndGnuSharedAttributeBytes(archiveStream);

            UName = TarHelpers.GetTrimmedAsciiString(_blocks._uNameBytes);
            GName = TarHelpers.GetTrimmedAsciiString(_blocks._gNameBytes);

            // DevMajor and DevMinor only have values with character devices and block devices.
            // For all other typeflags, the values in these fields are irrelevant.
            if (TypeFlag is TarEntryTypeFlag.Character or TarEntryTypeFlag.Block)
            {
                // Major number for a character device or block device entry.
                DevMajor = TarHelpers.GetTenBaseNumberFromOctalAsciiChars(_blocks._devMajorBytes);

                // Minor number for a character device or block device entry.
                DevMinor = TarHelpers.GetTenBaseNumberFromOctalAsciiChars(_blocks._devMinorBytes);
            }
        }

        // Reads attributes specific to the PAX format.
        // Throws if end of stream is reached.
        private void ReadPaxAttributes(Stream archiveStream)
        {
            // Pax does not use the prefix for extended paths like ustar.
            // Long paths are saved in the extended attributes section.
            _blocks.ReadPosixPrefixAttributeBytes(archiveStream);
            _blocks.ReadPosixPaddingBytes(archiveStream);
        }

        // Reads attributes specific to the GNU format.
        // Throws if end of stream is reached.
        private void ReadGnuAttributes(Stream archiveStream)
        {
            _blocks.ReadGnuAttributeBytes(archiveStream);
            _blocks.ReadGnuPaddingBytes(archiveStream);
        }

        // Reads the ustar prefix attribute.
        // Throws if end of stream is reached or if a conversion to an expected data type fails.
        private void ReadUstarAttributes(Stream archiveStream)
        {
            _blocks.ReadPosixPrefixAttributeBytes(archiveStream);

            Prefix = TarHelpers.GetTrimmedUtf8String(_blocks._prefixBytes);

            // In ustar, Prefix is used to store the *leading* path segments of
            // Name, if the full path did not fit in the Name byte array.
            if (!string.IsNullOrEmpty(Prefix))
            {
                Name = Path.Join(Prefix, Name);
            }

            _blocks.ReadPosixPaddingBytes(archiveStream);
        }

        // Collects the extended attributes found in the data section of a PAX entry of type 'x' or 'g'.
        // Throws if end of stream is reached or if an attribute is malformed.
        private void ReadPaxExtendedAttributes(Stream archiveStream)
        {
            Debug.Assert(TypeFlag is TarEntryTypeFlag.ExtendedAttributes or TarEntryTypeFlag.GlobalExtendedAttributes);

            if (Size > 0)
            {
                // Highly doubtful that a long path will be longer than int.MaxValue,
                // considering 4096 is a common max path length.
                Debug.Assert(Size <= int.MaxValue);

                _extendedAttributes ??= new();

                byte[] buffer = new byte[(int)Size];
                if (archiveStream.Read(buffer.AsSpan()) != Size)
                {
                    throw new EndOfStreamException();
                }

                string longPath = TarHelpers.GetTrimmedUtf8String(buffer);

                using StringReader reader = new(longPath);

                while (TryGetNextExtendedAttribute(reader, out string? key, out string? value))
                {
                    if (!string.IsNullOrEmpty(key) && !string.IsNullOrEmpty(value) && !_extendedAttributes.ContainsKey(key))
                    {
                        _extendedAttributes.Add(key, value);
                    }
                }
            }
        }

        // Tries to collect the next extended attribute from the string wrapped by the specified reader.
        // Extended attributes are saved in the format:
        // LENGTH KEY=VALUE\n
        // Where LENGTH is the total number of bytes of that line, from LENGTH itself to the endline, inclusive.
        // Throws if end of stream is reached or if an attribute is malformed.
        private bool TryGetNextExtendedAttribute(StringReader reader, out string? key, out string? value)
        {
            key = value = null;

            string? nextLine = reader.ReadLine();
            if (string.IsNullOrWhiteSpace(nextLine))
            {
                return false;
            }

            StringSplitOptions splitOptions = StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries;

            string[] attributeArray = nextLine.Split(' ', 2, splitOptions);
            if (attributeArray.Length != 2)
            {
                return false;
            }

            string[] keyAndValueArray = attributeArray[1].Split('=', 2, splitOptions);
            if (keyAndValueArray.Length != 2)
            {
                return false;
            }

            key = keyAndValueArray[0];
            value = keyAndValueArray[1];

            return true;
        }

        // Reads the long path found in the data section of a GNU entry of type 'K' or 'L'
        // and replaces Name or LinkName, respectively, with the found string.
        // Throws if end of stream is reached.
        private void ReadGnuLongPathDataBlock(Stream archiveStream)
        {
            Debug.Assert(TypeFlag is TarEntryTypeFlag.LongLink or TarEntryTypeFlag.LongPath);

            if (Size > 0)
            {
                // Highly doubtful that a long path will be longer than int.MaxValue.
                Debug.Assert(Size <= int.MaxValue);

                byte[] buffer = new byte[(int)Size];

                if (archiveStream.Read(buffer.AsSpan()) != Size)
                {
                    throw new EndOfStreamException();
                }

                string longPath = TarHelpers.GetTrimmedUtf8String(buffer);

                if (TypeFlag == TarEntryTypeFlag.LongLink)
                {
                    LinkName = longPath;
                }
                else if (TypeFlag == TarEntryTypeFlag.LongPath)
                {
                    Name = longPath;
                }
            }
        }

        // Reads the extended attributes found in the passed header, which should belong to an Extended Attributes entry,
        // and replaces any field of the current header with those found among the extended attributes, where it applies.
        // Throws if any conversion from string to the expected data type fails.
        private void ReplaceNormalAttributesWithExtended(TarHeader extendedAttributesHeader)
        {
            Debug.Assert(extendedAttributesHeader.TypeFlag == TarEntryTypeFlag.ExtendedAttributes);
            Debug.Assert(extendedAttributesHeader._extendedAttributes != null);

            Dictionary<string, string> ea = extendedAttributesHeader._extendedAttributes;

            if (ea.ContainsKey("ctime"))
            {
                double atime = double.Parse(ea["atime"]);
                ATime = TarHelpers.DateTimeFromSecondsSinceEpoch(atime);
            }
            if (ea.ContainsKey("ctime"))
            {
                double ctime = double.Parse(ea["ctime"]);
                CTime = TarHelpers.DateTimeFromSecondsSinceEpoch(ctime);
            }
            if (ea.ContainsKey("gid"))
            {
                Gid = int.Parse(ea["gid"]);
            }
            if (ea.ContainsKey("gname"))
            {
                GName = ea["gname"];
            }
            if (ea.ContainsKey("linkpath"))
            {
                LinkName = ea["linkpath"];
            }
            if (ea.ContainsKey("path"))
            {
                Name = ea["path"];
            }
            if (ea.ContainsKey("size"))
            {
                Size = long.Parse(ea["size"]);
            }
            if (ea.ContainsKey("uid"))
            {
                Uid = int.Parse(ea["uid"]);
            }
            if (ea.ContainsKey("uname"))
            {
                UName = ea["uname"];
            }

            _extendedAttributes ??= new();

            foreach ((string key, string value) in extendedAttributesHeader._extendedAttributes)
            {
                if (!_extendedAttributes.TryAdd(key, value))
                {
                    _extendedAttributes[key] = value;
                }
            }
        }

        // Depending on the specified previous header typeflag, replaces the current header's
        //  linkname or name with either the linkname or name of the previous header, respectively.
        private void ReplaceGnuPaths(TarHeader previousHeader)
        {
            Debug.Assert(previousHeader.TypeFlag is TarEntryTypeFlag.LongLink or TarEntryTypeFlag.LongPath);

            if (previousHeader.TypeFlag == TarEntryTypeFlag.LongLink)
            {
                LinkName = previousHeader.LinkName;
            }
            else if (previousHeader.TypeFlag == TarEntryTypeFlag.LongPath)
            {
                Name = previousHeader.Name;
            }
        }

        // After the file contents, there may be zero or more null characters,
        // which exist to ensure the data is aligned to the record size. Skip them and
        // set the stream position to the first byte of the next entry.
        private void SkipBlockAlignmentPadding(Stream archiveStream)
        {
            long ceilingMultipleOfRecordSize = ((TarArchive.RecordSize - 1) | (Size - 1)) + 1;
            int bufferLength = (int)(ceilingMultipleOfRecordSize - Size);

            if (archiveStream.CanSeek)
            {
                archiveStream.AdvanceToPosition(archiveStream.Position + bufferLength);
                _endOfHeader = archiveStream.Position;
            }
            else if (bufferLength > 0)
            {
                byte[] buffer = ArrayPool<byte>.Shared.Rent(minimumLength: bufferLength);
                if (archiveStream.Read(buffer.AsSpan(0, bufferLength)) != bufferLength)
                {
                    throw new EndOfStreamException();
                }
                ArrayPool<byte>.Shared.Return(buffer);
            }
        }

        // Move the stream position to the first byte after the data ends.
        private void ProcessDataBlock(Stream archiveStream)
        {
            if ((TypeFlag is TarEntryTypeFlag.Normal or TarEntryTypeFlag.OldNormal) && Size > 0)
            {
                _dataStream = GetDataStream(archiveStream);
            }
            else if (TypeFlag is TarEntryTypeFlag.ExtendedAttributes or TarEntryTypeFlag.GlobalExtendedAttributes)
            {
                ReadPaxExtendedAttributes(archiveStream);
            }
            else if (TypeFlag is TarEntryTypeFlag.LongLink or TarEntryTypeFlag.LongPath)
            {
                ReadGnuLongPathDataBlock(archiveStream);
            }
            else
            {
                // Anything that is not a normal file does not have actual data.
                DiscardBytes(archiveStream, Size);
            }
        }

        // Returns a stream that represents the data section of the current header.
        private Stream GetDataStream(Stream archiveStream)
        {
            Stream stream;

            if (archiveStream.CanSeek)
            {
                long dataStartPosition = archiveStream.Position;
                stream = new SeekableSubReadStream(archiveStream, dataStartPosition, Size);
                archiveStream.Position += Size;
            }
            else
            {
                stream = CopyDataToNewStream(archiveStream, Size);
            }

            return stream;
        }

        // Copies the data section of the current header into a new stream, then returns it.
        private Stream CopyDataToNewStream(Stream archiveStream, long bytesToRead)
        {
            MemoryStream stream = new();

            int bufferLength = 4096;
            byte[] buffer = ArrayPool<byte>.Shared.Rent(minimumLength: bufferLength);
            while (bytesToRead > 0)
            {
                if (bytesToRead > int.MaxValue)
                {
                    if (archiveStream.Read(buffer.AsSpan(0, bufferLength)) != bufferLength)
                    {
                        throw new EndOfStreamException();
                    }
                    stream.Write(buffer.AsSpan());
                    bytesToRead -= bufferLength;
                }
                else
                {
                    if (archiveStream.Read(buffer.AsSpan(0, (int)bytesToRead)) != bytesToRead)
                    {
                        throw new EndOfStreamException();
                    }
                    stream.Write(buffer.AsSpan(0, (int)bytesToRead));
                    bytesToRead = 0;
                }
            }
            ArrayPool<byte>.Shared.Return(buffer);

            return stream;
        }

        // Skips the specified bytes from the archive stream.
        private void DiscardBytes(Stream archiveStream, long bytesToDiscard)
        {
            if (bytesToDiscard == 0)
            {
                return;
            }

            int bufferLength = 4096;
            byte[] buffer = new byte[bufferLength];
            long bytesDiscarded = 0;

            while (bytesDiscarded < bytesToDiscard)
            {
                if (bytesToDiscard > int.MaxValue)
                {
                    if (archiveStream.Read(buffer.AsSpan(0, bufferLength)) != bufferLength)
                    {
                        throw new EndOfStreamException();
                    }
                    bytesDiscarded += bufferLength;
                }
                else
                {
                    if (archiveStream.Read(buffer.AsSpan(0, (int)bytesToDiscard)) != bytesToDiscard)
                    {
                        throw new EndOfStreamException();
                    }
                    bytesDiscarded += bytesToDiscard;
                }
            }
        }
    }
}
