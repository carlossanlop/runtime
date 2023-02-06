// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.IO.Compression;
using System.Text;

namespace System.Formats.Zip;

// Zip Spec: http://www.pkware.com/documents/casestudies/APPNOTE.TXT

public class ZipReader
{
    private bool _isDisposed;
    private bool _leaveOpen;
    private bool _readEntries;
    private uint _numberOfThisDisk; //only valid after ReadCentralDirectory
    private long _centralDirectoryStart;
    private long _expectedNumberOfEntries;
    private byte[] _archiveComment;

    private BinaryReader? _archiveReader;
    private Stream _archiveStream;
    private List<ZipEntry> _entries;
    private ReadOnlyCollection<ZipEntry> _entriesCollection;
    private Dictionary<string, ZipEntry> _entriesDictionary;
    private Encoding? _entryNameAndCommentEncoding;

    public ZipReader(Stream archiveStream, Encoding? entryNameAndCommentEncoding, bool leaveOpen)
    {
        ArgumentNullException.ThrowIfNull(archiveStream);
        EntryNameAndCommentEncoding = entryNameAndCommentEncoding;

        if (!archiveStream.CanRead)
            throw new ArgumentException(SR.ReadModeCapabilities);

        _archiveStream = archiveStream;

        _archiveReader = new BinaryReader(_archiveStream);
        _entries = new List<ZipEntry>();
        _entriesCollection = new ReadOnlyCollection<ZipEntry>(_entries);
        _entriesDictionary = new Dictionary<string, ZipEntry>();
        _readEntries = false;
        _leaveOpen = leaveOpen;
        _centralDirectoryStart = 0; // invalid until ReadCentralDirectory
        _isDisposed = false;
        _numberOfThisDisk = 0; // invalid until ReadCentralDirectory
        _archiveComment = Array.Empty<byte>();

        ReadEndOfCentralDirectory();
    }

    [AllowNull]
    public string Comment => (EntryNameAndCommentEncoding ?? Encoding.UTF8).GetString(_archiveComment);

    public ReadOnlyCollection<ZipEntry> Entries
    {
        get
        {
            ThrowIfDisposed();
            EnsureCentralDirectoryRead();
            return _entriesCollection;
        }
    }

    internal Encoding? EntryNameAndCommentEncoding
    {
        get => _entryNameAndCommentEncoding;

        private set
        {
            // value == null is fine. This means the user does not want to overwrite default encoding picking logic.

            // The Zip file spec [http://www.pkware.com/documents/casestudies/APPNOTE.TXT] specifies a bit in the entry header
            // (specifically: the language encoding flag (EFS) in the general purpose bit flag of the local file header) that
            // basically says: UTF8 (1) or CP437 (0). But in reality, tools replace CP437 with "something else that is not UTF8".
            // For instance, the Windows Shell Zip tool takes "something else" to mean "the local system codepage".
            // We default to the same behaviour, but we let the user explicitly specify the encoding to use for cases where they
            // understand their use case well enough.
            // Since the definition of acceptable encodings for the "something else" case is in reality by convention, it is not
            // immediately clear, whether non-UTF8 Unicode encodings are acceptable. To determine that we would need to survey
            // what is currently being done in the field, but we do not have the time for it right now.
            // So, we artificially disallow non-UTF8 Unicode encodings for now to make sure we are not creating a compat burden
            // for something other tools do not support. If we realise in future that "something else" should include non-UTF8
            // Unicode encodings, we can remove this restriction.

            if (value != null &&
                    (value.Equals(Encoding.BigEndianUnicode)
                    || value.Equals(Encoding.Unicode)))
            {
                throw new ArgumentException(SR.EntryNameAndCommentEncodingNotSupported, nameof(EntryNameAndCommentEncoding));
            }

            _entryNameAndCommentEncoding = value;
        }
    }

    internal uint NumberOfThisDisk => _numberOfThisDisk;

    public void Dispose()
    {
        if (!_isDisposed)
        {
            _isDisposed = true;
            if (!_leaveOpen)
            {
                _archiveStream.Dispose();
            }
        }
    }

    public ZipEntry? GetEntry(string entryName)
    {
        ArgumentNullException.ThrowIfNull(entryName);

        EnsureCentralDirectoryRead();
        _entriesDictionary.TryGetValue(entryName, out ZipEntry? result);
        return result;
    }

    internal void ThrowIfDisposed() => ObjectDisposedException.ThrowIf(_isDisposed, this);

    private void EnsureCentralDirectoryRead()
    {
        if (!_readEntries)
        {
            ReadCentralDirectory();
            _readEntries = true;
        }
    }

    private void ReadCentralDirectory()
    {
        try
        {
            // assume ReadEndOfCentralDirectory has been called and has populated _centralDirectoryStart

            _archiveStream.Seek(_centralDirectoryStart, SeekOrigin.Begin);

            long numberOfEntries = 0;

            Debug.Assert(_archiveReader != null);
            //read the central directory
            ZipCentralDirectoryFileHeader currentHeader;
            while (ZipCentralDirectoryFileHeader.TryReadBlock(_archiveReader, saveExtraFieldsAndComments: false, out currentHeader))
            {
                AddEntry(new ZipEntry(this, currentHeader));
                numberOfEntries++;
            }

            if (numberOfEntries != _expectedNumberOfEntries)
                throw new InvalidDataException(SR.NumEntriesWrong);
        }
        catch (EndOfStreamException ex)
        {
            throw new InvalidDataException(SR.Format(SR.CentralDirectoryInvalid, ex));
        }
    }

    private void AddEntry(ZipEntry entry)
    {
        _entries.Add(entry);
        _entriesDictionary.TryAdd(entry.FullName, entry);
    }

    // This function reads all the EOCD stuff it needs to find the offset to the start of the central directory
    // This offset gets put in _centralDirectoryStart and the number of this disk gets put in _numberOfThisDisk
    // Also does some verification that this isn't a split/spanned archive
    // Also checks that offset to CD isn't out of bounds
    private void ReadEndOfCentralDirectory()
    {
        try
        {
            // This seeks backwards almost to the beginning of the EOCD, one byte after where the signature would be
            // located if the EOCD had the minimum possible size (no file zip comment)
            _archiveStream.Seek(-ZipEndOfCentralDirectoryBlock.SizeOfBlockWithoutSignature, SeekOrigin.End);

            // If the EOCD has the minimum possible size (no zip file comment), then exactly the previous 4 bytes will contain the signature
            // But if the EOCD has max possible size, the signature should be found somewhere in the previous 64K + 4 bytes
            if (!ZipHelper.SeekBackwardsToSignature(_archiveStream,
                    ZipEndOfCentralDirectoryBlock.SignatureConstant,
                    ZipEndOfCentralDirectoryBlock.ZipFileCommentMaxLength + ZipEndOfCentralDirectoryBlock.SignatureSize))
                throw new InvalidDataException(SR.EOCDNotFound);

            long eocdStart = _archiveStream.Position;

            Debug.Assert(_archiveReader != null);
            // read the EOCD
            ZipEndOfCentralDirectoryBlock eocd;
            bool eocdProper = ZipEndOfCentralDirectoryBlock.TryReadBlock(_archiveReader, out eocd);
            Debug.Assert(eocdProper); // we just found this using the signature finder, so it should be okay

            if (eocd.NumberOfThisDisk != eocd.NumberOfTheDiskWithTheStartOfTheCentralDirectory)
                throw new InvalidDataException(SR.SplitSpanned);

            _numberOfThisDisk = eocd.NumberOfThisDisk;
            _centralDirectoryStart = eocd.OffsetOfStartOfCentralDirectoryWithRespectToTheStartingDiskNumber;

            if (eocd.NumberOfEntriesInTheCentralDirectory != eocd.NumberOfEntriesInTheCentralDirectoryOnThisDisk)
                throw new InvalidDataException(SR.SplitSpanned);

            _expectedNumberOfEntries = eocd.NumberOfEntriesInTheCentralDirectory;

            _archiveComment = eocd.ArchiveComment;

            TryReadZip64EndOfCentralDirectory(eocd, eocdStart);

            if (_centralDirectoryStart > _archiveStream.Length)
            {
                throw new InvalidDataException(SR.FieldTooBigOffsetToCD);
            }
        }
        catch (EndOfStreamException ex)
        {
            throw new InvalidDataException(SR.CDCorrupt, ex);
        }
        catch (IOException ex)
        {
            throw new InvalidDataException(SR.CDCorrupt, ex);
        }
    }

    // Tries to find the Zip64 End of Central Directory Locator, then the Zip64 End of Central Directory, assuming the
    // End of Central Directory block has already been found, as well as the location in the stream where the EOCD starts.
    private void TryReadZip64EndOfCentralDirectory(ZipEndOfCentralDirectoryBlock eocd, long eocdStart)
    {
        // Only bother looking for the Zip64-EOCD stuff if we suspect it is needed because some value is FFFFFFFFF
        // because these are the only two values we need, we only worry about these
        // if we don't find the Zip64-EOCD, we just give up and try to use the original values
        if (eocd.NumberOfThisDisk == ZipHelper.Mask16Bit ||
            eocd.OffsetOfStartOfCentralDirectoryWithRespectToTheStartingDiskNumber == ZipHelper.Mask32Bit ||
            eocd.NumberOfEntriesInTheCentralDirectory == ZipHelper.Mask16Bit)
        {
            // Read Zip64 End of Central Directory Locator

            // This seeks forwards almost to the beginning of the Zip64-EOCDL, one byte after where the signature would be located
            _archiveStream.Seek(eocdStart - Zip64EndOfCentralDirectoryLocator.SizeOfBlockWithoutSignature, SeekOrigin.Begin);

            // Exactly the previous 4 bytes should contain the Zip64-EOCDL signature
            // if we don't find it, assume it doesn't exist and use data from normal EOCD
            if (ZipHelper.SeekBackwardsToSignature(_archiveStream,
                    Zip64EndOfCentralDirectoryLocator.SignatureConstant,
                    Zip64EndOfCentralDirectoryLocator.SignatureSize))
            {
                Debug.Assert(_archiveReader != null);

                // use locator to get to Zip64-EOCD
                Zip64EndOfCentralDirectoryLocator locator;
                bool zip64eocdLocatorProper = Zip64EndOfCentralDirectoryLocator.TryReadBlock(_archiveReader, out locator);
                Debug.Assert(zip64eocdLocatorProper); // we just found this using the signature finder, so it should be okay

                if (locator.OffsetOfZip64EOCD > long.MaxValue)
                    throw new InvalidDataException(SR.FieldTooBigOffsetToZip64EOCD);

                long zip64EOCDOffset = (long)locator.OffsetOfZip64EOCD;

                _archiveStream.Seek(zip64EOCDOffset, SeekOrigin.Begin);

                // Read Zip64 End of Central Directory Record

                Zip64EndOfCentralDirectoryRecord record;
                if (!Zip64EndOfCentralDirectoryRecord.TryReadBlock(_archiveReader, out record))
                    throw new InvalidDataException(SR.Zip64EOCDNotWhereExpected);

                _numberOfThisDisk = record.NumberOfThisDisk;

                if (record.NumberOfEntriesTotal > long.MaxValue)
                    throw new InvalidDataException(SR.FieldTooBigNumEntries);

                if (record.OffsetOfCentralDirectory > long.MaxValue)
                    throw new InvalidDataException(SR.FieldTooBigOffsetToCD);

                if (record.NumberOfEntriesTotal != record.NumberOfEntriesOnThisDisk)
                    throw new InvalidDataException(SR.SplitSpanned);

                _expectedNumberOfEntries = (long)record.NumberOfEntriesTotal;
                _centralDirectoryStart = (long)record.OffsetOfCentralDirectory;
            }
        }
    }
}
