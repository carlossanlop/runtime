// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace System.IO.Compression;

public partial class ZipArchive : IDisposable, IAsyncDisposable
{
    // This would be the counterpart of the Entries synchronous property.
    public async Task<ReadOnlyCollection<ZipArchiveEntry>> GetEntriesAsync(CancellationToken cancellationToken)
    {
        if (_mode == ZipArchiveMode.Create)
            throw new NotSupportedException(SR.EntriesInCreateMode);

        ThrowIfDisposed();

        await EnsureCentralDirectoryReadAsync(cancellationToken).ConfigureAwait(false);
        return _entriesCollection;
    }

    public async ValueTask DisposeAsync()
    {
        if (!_isDisposed)
        {
            try
            {
                switch (_mode)
                {
                    case ZipArchiveMode.Read:
                        break;
                    case ZipArchiveMode.Create:
                    case ZipArchiveMode.Update:
                    default:
                        Debug.Assert(_mode == ZipArchiveMode.Update || _mode == ZipArchiveMode.Create);
                        await WriteFileAsync().ConfigureAwait(false);
                        break;
                }
            }
            finally
            {
                await CloseStreamsAsync().ConfigureAwait(false);
                _isDisposed = true;
            }
        }
    }

    /// <summary>
    /// Asynchronously retrieves a wrapper for the file entry in the archive with the specified name. Names are compared using ordinal comparison. If there are multiple entries in the archive with the specified name, the first one found will be returned.
    /// </summary>
    /// <exception cref="ArgumentException">entryName is a zero-length string.</exception>
    /// <exception cref="ArgumentNullException">entryName is null.</exception>
    /// <exception cref="NotSupportedException">The ZipArchive does not support reading.</exception>
    /// <exception cref="ObjectDisposedException">The ZipArchive has already been closed.</exception>
    /// <exception cref="InvalidDataException">The Zip archive is corrupt and the entries cannot be retrieved.</exception>
    /// <param name="entryName">A path relative to the root of the archive, identifying the desired entry.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>A wrapper for the file entry in the archive. If no entry in the archive exists with the specified name, null will be returned.</returns>
    public async Task<ZipArchiveEntry?> GetEntryAsync(string entryName, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(entryName);

        if (_mode == ZipArchiveMode.Create)
            throw new NotSupportedException(SR.EntriesInCreateMode);

        await EnsureCentralDirectoryReadAsync(cancellationToken).ConfigureAwait(false);
        _entriesDictionary.TryGetValue(entryName, out ZipArchiveEntry? result);
        return result;
    }

    internal async Task AcquireArchiveStreamAsync(ZipArchiveEntry entry, CancellationToken cancellationToken)
    {
        // if a previous entry had held the stream but never wrote anything, we write their local header for them
        if (_archiveStreamOwner != null)
        {
            if (!_archiveStreamOwner.EverOpenedForWrite)
            {
                await _archiveStreamOwner.WriteAndFinishLocalEntryAsync(forceWrite: true, cancellationToken).ConfigureAwait(false);
            }
            else
            {
                throw new IOException(SR.CreateModeCreateEntryWhileOpen);
            }
        }

        _archiveStreamOwner = entry;
    }

    private async Task CloseStreamsAsync()
    {
        if (!_leaveOpen)
        {
            await _archiveStream.DisposeAsync().ConfigureAwait(false);
            if (_backingStream != null)
            {
                await _backingStream.DisposeAsync().ConfigureAwait(false);
            }
        }
        else
        {
            // if _backingStream isn't null, that means we assigned the original stream they passed
            // us to _backingStream (which they requested we leave open), and _archiveStream was
            // the temporary copy that we needed
            if (_backingStream != null)
            {
                await _archiveStream.DisposeAsync().ConfigureAwait(false);
            }
        }
    }

    private async Task EnsureCentralDirectoryReadAsync(CancellationToken cancellationToken)
    {
        if (!_readEntries)
        {
            await ReadCentralDirectoryAsync(cancellationToken).ConfigureAwait(false);
            _readEntries = true;
        }
    }

    private async Task ReadCentralDirectoryAsync(CancellationToken cancellationToken)
    {
        const int ReadBufferSize = 4096;

        byte[] fileBuffer = Buffers.ArrayPool<byte>.Shared.Rent(ReadBufferSize);
        Memory<byte> fileBufferMemory = fileBuffer.AsMemory(0, ReadBufferSize);

        try
        {
            // assume ReadEndOfCentralDirectory has been called and has populated _centralDirectoryStart

            _archiveStream.Seek(_centralDirectoryStart, SeekOrigin.Begin);

            long numberOfEntries = 0;
            bool saveExtraFieldsAndComments = Mode == ZipArchiveMode.Update;

            bool continueReadingCentralDirectory = true;
            // total bytes read from central directory
            int bytesRead = 0;
            // current position in the current buffer
            int currPosition = 0;
            // total bytes read from all file headers starting in the current buffer
            int bytesConsumed = 0;

            _entries.Clear();
            _entriesDictionary.Clear();

            // read the central directory
            while (continueReadingCentralDirectory)
            {
                int currBytesRead = await _archiveStream.ReadAsync(fileBufferMemory, cancellationToken).ConfigureAwait(false);
                ReadOnlyMemory<byte> sizedFileBuffer = fileBufferMemory.Slice(0, currBytesRead);

                // the buffer read must always be large enough to fit the constant section size of at least one header
                continueReadingCentralDirectory = continueReadingCentralDirectory
                    && sizedFileBuffer.Length >= ZipCentralDirectoryFileHeader.BlockConstantSectionSize;

                while (continueReadingCentralDirectory
                    && currPosition + ZipCentralDirectoryFileHeader.BlockConstantSectionSize < sizedFileBuffer.Length)
                {
                    ZipCentralDirectoryFileHeader currentHeader = new();

                    if (continueReadingCentralDirectory)
                    {
                        (continueReadingCentralDirectory, bytesConsumed, currentHeader) =
                            await ZipCentralDirectoryFileHeader.TryReadBlockAsync(sizedFileBuffer.Slice(currPosition), _archiveStream,
                       saveExtraFieldsAndComments).ConfigureAwait(false);
                    }

                    if (!continueReadingCentralDirectory)
                    {
                        break;
                    }

                    AddEntry(new ZipArchiveEntry(this, currentHeader));
                    numberOfEntries++;
                    if (numberOfEntries > _expectedNumberOfEntries)
                    {
                        throw new InvalidDataException(SR.NumEntriesWrong);
                    }

                    currPosition += bytesConsumed;
                    bytesRead += bytesConsumed;
                }

                // We've run out of possible space in the entry - seek backwards by the number of bytes remaining in
                // this buffer (so that the next buffer overlaps with this one) and retry.
                if (currPosition < sizedFileBuffer.Length)
                {
                    _archiveStream.Seek(-(sizedFileBuffer.Length - currPosition), SeekOrigin.Current);
                }
                currPosition = 0;
            }

            if (numberOfEntries != _expectedNumberOfEntries)
            {
                throw new InvalidDataException(SR.NumEntriesWrong);
            }

            // Sort _entries by each archive entry's position. This supports the algorithm in WriteFile, so is only
            // necessary when the ZipArchive has been opened in Update mode.
            if (Mode == ZipArchiveMode.Update)
            {
                _entries.Sort(ZipArchiveEntry.LocalHeaderOffsetComparer.Instance);
            }
        }
        catch (EndOfStreamException ex)
        {
            throw new InvalidDataException(SR.Format(SR.CentralDirectoryInvalid, ex));
        }
        finally
        {
            Buffers.ArrayPool<byte>.Shared.Return(fileBuffer);
        }
    }

    // This function reads all the EOCD stuff it needs to find the offset to the start of the central directory
    // This offset gets put in _centralDirectoryStart and the number of this disk gets put in _numberOfThisDisk
    // Also does some verification that this isn't a split/spanned archive
    // Also checks that offset to CD isn't out of bounds
    private async Task ReadEndOfCentralDirectoryAsync(CancellationToken cancellationToken)
    {
        try
        {
            // This seeks backwards almost to the beginning of the EOCD, one byte after where the signature would be
            // located if the EOCD had the minimum possible size (no file zip comment)
            _archiveStream.Seek(-ZipEndOfCentralDirectoryBlock.SizeOfBlockWithoutSignature, SeekOrigin.End);

            // If the EOCD has the minimum possible size (no zip file comment), then exactly the previous 4 bytes will contain the signature
            // But if the EOCD has max possible size, the signature should be found somewhere in the previous 64K + 4 bytes
            if (!await ZipHelper.SeekBackwardsToSignatureAsync(_archiveStream,
                    ZipEndOfCentralDirectoryBlock.SignatureConstantBytes,
                    ZipEndOfCentralDirectoryBlock.ZipFileCommentMaxLength + ZipEndOfCentralDirectoryBlock.FieldLengths.Signature, cancellationToken).ConfigureAwait(false))
                throw new InvalidDataException(SR.EOCDNotFound);

            long eocdStart = _archiveStream.Position;

            // read the EOCD
            (bool eocdProper, ZipEndOfCentralDirectoryBlock eocd) = await ZipEndOfCentralDirectoryBlock.TryReadBlockAsync(_archiveStream, cancellationToken).ConfigureAwait(false);
            Debug.Assert(eocdProper); // we just found this using the signature finder, so it should be okay

            if (eocd.NumberOfThisDisk != eocd.NumberOfTheDiskWithTheStartOfTheCentralDirectory)
                throw new InvalidDataException(SR.SplitSpanned);

            _numberOfThisDisk = eocd.NumberOfThisDisk;
            _centralDirectoryStart = eocd.OffsetOfStartOfCentralDirectoryWithRespectToTheStartingDiskNumber;

            if (eocd.NumberOfEntriesInTheCentralDirectory != eocd.NumberOfEntriesInTheCentralDirectoryOnThisDisk)
                throw new InvalidDataException(SR.SplitSpanned);

            _expectedNumberOfEntries = eocd.NumberOfEntriesInTheCentralDirectory;

            _archiveComment = eocd.ArchiveComment;

            await TryReadZip64EndOfCentralDirectoryAsync(eocd, eocdStart, cancellationToken).ConfigureAwait(false);

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
    private async Task TryReadZip64EndOfCentralDirectoryAsync(ZipEndOfCentralDirectoryBlock eocd, long eocdStart, CancellationToken cancellationToken)
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
            if (await ZipHelper.SeekBackwardsToSignatureAsync(_archiveStream,
                    Zip64EndOfCentralDirectoryLocator.SignatureConstantBytes,
                    Zip64EndOfCentralDirectoryLocator.FieldLengths.Signature, cancellationToken).ConfigureAwait(false))
            {
                // use locator to get to Zip64-EOCD
                (bool zip64eocdLocatorProper, Zip64EndOfCentralDirectoryLocator locator) = await Zip64EndOfCentralDirectoryLocator.TryReadBlockAsync(_archiveStream, cancellationToken).ConfigureAwait(false);
                Debug.Assert(zip64eocdLocatorProper); // we just found this using the signature finder, so it should be okay

                if (locator.OffsetOfZip64EOCD > long.MaxValue)
                    throw new InvalidDataException(SR.FieldTooBigOffsetToZip64EOCD);

                long zip64EOCDOffset = (long)locator.OffsetOfZip64EOCD;

                _archiveStream.Seek(zip64EOCDOffset, SeekOrigin.Begin);

                // Read Zip64 End of Central Directory Record

                (bool result, Zip64EndOfCentralDirectoryRecord record) = await Zip64EndOfCentralDirectoryRecord.TryReadBlockAsync(_archiveStream, cancellationToken).ConfigureAwait(false);
                if (!result)
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

    private async Task WriteFileAsync(CancellationToken cancellationToken = default)
    {
        // if we are in create mode, we always set readEntries to true in Init
        // if we are in update mode, we call EnsureCentralDirectoryRead, which sets readEntries to true
        Debug.Assert(_readEntries);

        // Entries starting after this offset have had a dynamically-sized change. Everything on or after this point must be rewritten.
        long completeRewriteStartingOffset = 0;
        List<ZipArchiveEntry> entriesToWrite = _entries;

        if (_mode == ZipArchiveMode.Update)
        {
            // Entries starting after this offset have some kind of change made to them. It might just be a fixed-length field though, in which case
            // that single entry's metadata can be rewritten without impacting anything else.
            long startingOffset = _firstDeletedEntryOffset;
            long nextFileOffset = 0;
            completeRewriteStartingOffset = startingOffset;

            entriesToWrite = new(_entries.Count);
            foreach (ZipArchiveEntry entry in _entries)
            {
                if (!entry.OriginallyInArchive)
                {
                    entriesToWrite.Add(entry);
                }
                else
                {
                    if (entry.Changes == ChangeState.Unchanged)
                    {
                        // Keep track of the expected position of the file entry after the final untouched file entry so that when the loop completes,
                        // we'll know which position to start writing new entries from.
                        long offsetOfCompressedData = await entry.GetOffsetOfCompressedDataAsync(cancellationToken).ConfigureAwait(false);
                        nextFileOffset = Math.Max(nextFileOffset, offsetOfCompressedData + entry.CompressedLength);
                    }
                    // When calculating the starting offset to load the files from, only look at changed entries which are already in the archive.
                    else
                    {
                        startingOffset = Math.Min(startingOffset, entry.OffsetOfLocalHeader);
                    }

                    // We want to re-write entries which are after the starting offset of the first entry which has pending data to write.
                    // NB: the existing ZipArchiveEntries are sorted in _entries by their position ascending.
                    if (entry.OffsetOfLocalHeader >= startingOffset)
                    {
                        // If the pending data to write is fixed-length metadata in the header, there's no need to load the compressed file bits.
                        if ((entry.Changes & (ChangeState.DynamicLengthMetadata | ChangeState.StoredData)) != 0)
                        {
                            completeRewriteStartingOffset = Math.Min(completeRewriteStartingOffset, entry.OffsetOfLocalHeader);
                        }
                        if (entry.OffsetOfLocalHeader >= completeRewriteStartingOffset)
                        {
                            await entry.LoadLocalHeaderExtraFieldAndCompressedBytesIfNeededAsync(cancellationToken).ConfigureAwait(false);
                        }

                        entriesToWrite.Add(entry);
                    }
                }
            }

            // If the offset of entries to write from is still at long.MaxValue, then we know that nothing has been deleted,
            // nothing has been modified - so we just want to move to the end of all remaining files in the archive.
            if (startingOffset == long.MaxValue)
            {
                startingOffset = nextFileOffset;
            }

            _archiveStream.Seek(startingOffset, SeekOrigin.Begin);
        }

        foreach (ZipArchiveEntry entry in entriesToWrite)
        {
            // We don't always need to write the local header entry, ZipArchiveEntry is usually able to work out when it doesn't need to.
            // We want to force this header entry to be written (even for completely untouched entries) if the entry comes after one
            // which had a pending dynamically-sized write.
            bool forceWriteLocalEntry = !entry.OriginallyInArchive || (entry.OriginallyInArchive && entry.OffsetOfLocalHeader >= completeRewriteStartingOffset);

            await entry.WriteAndFinishLocalEntryAsync(forceWriteLocalEntry, cancellationToken).ConfigureAwait(false);
        }

        long plannedCentralDirectoryPosition = _archiveStream.Position;
        // If there are no entries in the archive, we still want to create the archive epilogue.
        bool archiveEpilogueRequiresUpdate = _entries.Count == 0;

        foreach (ZipArchiveEntry entry in _entries)
        {
            // The central directory needs to be rewritten if its position has moved, if there's a new entry in the archive, or if the entry might be different.
            bool centralDirectoryEntryRequiresUpdate = plannedCentralDirectoryPosition != _centralDirectoryStart
                || !entry.OriginallyInArchive || entry.OffsetOfLocalHeader >= completeRewriteStartingOffset;

            await entry.WriteCentralDirectoryFileHeaderAsync(centralDirectoryEntryRequiresUpdate, cancellationToken).ConfigureAwait(false);
            archiveEpilogueRequiresUpdate |= centralDirectoryEntryRequiresUpdate;
        }

        long sizeOfCentralDirectory = _archiveStream.Position - plannedCentralDirectoryPosition;

        await WriteArchiveEpilogueAsync(plannedCentralDirectoryPosition, sizeOfCentralDirectory, archiveEpilogueRequiresUpdate, cancellationToken).ConfigureAwait(false);

        // If entries have been removed and new (smaller) ones added, there could be empty space at the end of the file.
        // Shrink the file to reclaim this space.
        if (_mode == ZipArchiveMode.Update && _archiveStream.Position != _archiveStream.Length)
        {
            _archiveStream.SetLength(_archiveStream.Position);
        }
    }

    // writes eocd, and if needed, zip 64 eocd, zip64 eocd locator
    // should only throw an exception in extremely exceptional cases because it is called from dispose
    private async Task WriteArchiveEpilogueAsync(long startOfCentralDirectory, long sizeOfCentralDirectory, bool centralDirectoryChanged, CancellationToken cancellationToken)
    {
        // determine if we need Zip 64
        if (startOfCentralDirectory >= uint.MaxValue
            || sizeOfCentralDirectory >= uint.MaxValue
            || _entries.Count >= ZipHelper.Mask16Bit
#if DEBUG_FORCE_ZIP64
                || _forceZip64
#endif
            )
        {
            // if we need zip 64, write zip 64 eocd and locator
            long zip64EOCDRecordStart = _archiveStream.Position;

            if (centralDirectoryChanged)
            {
                await Zip64EndOfCentralDirectoryRecord.WriteBlockAsync(_archiveStream, _entries.Count, startOfCentralDirectory, sizeOfCentralDirectory, cancellationToken).ConfigureAwait(false);
                await Zip64EndOfCentralDirectoryLocator.WriteBlockAsync(_archiveStream, zip64EOCDRecordStart, cancellationToken).ConfigureAwait(false);
            }
            else
            {
                _archiveStream.Seek(Zip64EndOfCentralDirectoryRecord.TotalSize, SeekOrigin.Current);
                _archiveStream.Seek(Zip64EndOfCentralDirectoryLocator.TotalSize, SeekOrigin.Current);
            }
        }

        // write normal eocd
        if (centralDirectoryChanged || (Changed != ChangeState.Unchanged))
        {
            await ZipEndOfCentralDirectoryBlock.WriteBlockAsync(_archiveStream, _entries.Count, startOfCentralDirectory, sizeOfCentralDirectory, _archiveComment, cancellationToken).ConfigureAwait(false);
        }
        else
        {
            _archiveStream.Seek(ZipEndOfCentralDirectoryBlock.TotalSize + _archiveComment.Length, SeekOrigin.Current);
        }
    }
}
