// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.IO.Compression;
using System.Text;

namespace System.Formats.Zip;

// Zip Spec: http://www.pkware.com/documents/casestudies/APPNOTE.TXT

public partial class ZipEntry
{
    private bool _isEncrypted;
    private readonly int _diskNumberStart;
    private readonly ZipVersionMadeByPlatform _versionMadeByPlatform;
    private ZipVersionNeededValues _versionMadeBySpecification;
    internal ZipVersionNeededValues _versionToExtract;
    private BitFlagValues _generalPurposeBitFlag;
    private CompressionMethodValues _storedCompressionMethod;
    private DateTimeOffset _lastModified;

    private long _compressedSize;
    private long _uncompressedSize;
    private long _offsetOfLocalHeader;
    private long? _storedOffsetOfCompressedData;
    private uint _crc32;
    // An array of buffers, each a maximum of MaxSingleBufferSize in size
    private byte[][]? _compressedBytes;
    private MemoryStream? _storedUncompressedData;
    private bool _currentlyOpenForWrite;
    private bool _everOpenedForWrite;
    private Stream? _outstandingWriteStream;
    private uint _externalFileAttr;
    private string _storedEntryName;
    private byte[] _storedEntryNameBytes;
    // only apply to update mode
    private List<ZipGenericExtraField>? _cdUnknownExtraFields;
    private List<ZipGenericExtraField>? _lhUnknownExtraFields;
    private byte[] _fileComment;
    private readonly CompressionLevel? _compressionLevel;
    private ZipReader? _readerOfOrigin;

    private static readonly bool s_allowLargeZipArchiveEntriesInUpdateMode = IntPtr.Size > 4;

    internal ZipEntry(ZipReader readerOfOrigin, ZipCentralDirectoryFileHeader cd)
    {
        _readerOfOrigin = readerOfOrigin;

        _diskNumberStart = cd.DiskNumberStart;
        _versionMadeByPlatform = (ZipVersionMadeByPlatform)cd.VersionMadeByCompatibility;
        _versionMadeBySpecification = (ZipVersionNeededValues)cd.VersionMadeBySpecification;
        _versionToExtract = (ZipVersionNeededValues)cd.VersionNeededToExtract;
        _generalPurposeBitFlag = (BitFlagValues)cd.GeneralPurposeBitFlag;
        _isEncrypted = (_generalPurposeBitFlag & BitFlagValues.IsEncrypted) != 0;
        CompressionMethod = (CompressionMethodValues)cd.CompressionMethod;
        _lastModified = new DateTimeOffset(ZipHelper.DosTimeToDateTime(cd.LastModified));
        _compressedSize = cd.CompressedSize;
        _uncompressedSize = cd.UncompressedSize;
        _externalFileAttr = cd.ExternalFileAttributes;
        _offsetOfLocalHeader = cd.RelativeOffsetOfLocalHeader;
        // we don't know this yet: should be _offsetOfLocalHeader + 30 + _storedEntryNameBytes.Length + extrafieldlength
        // but entryname/extra length could be different in LH
        _storedOffsetOfCompressedData = null;
        _crc32 = cd.Crc32;

        _compressedBytes = null;
        _storedUncompressedData = null;
        _currentlyOpenForWrite = false;
        _everOpenedForWrite = false;
        _outstandingWriteStream = null;

        _storedEntryNameBytes = cd.Filename;
        _storedEntryName = (_readerOfOrigin.EntryNameAndCommentEncoding ?? Encoding.UTF8).GetString(_storedEntryNameBytes);
        DetectEntryNameVersion();

        _lhUnknownExtraFields = null;
        // the cd should have this as null if we aren't in Update mode
        _cdUnknownExtraFields = cd.ExtraFields;

        _fileComment = cd.FileComment;

        _compressionLevel = null;
    }

    internal ZipEntry(string entryName, CompressionLevel compressionLevel)
        : this(entryName)
    {
        _compressionLevel = compressionLevel;
        if (_compressionLevel == CompressionLevel.NoCompression)
        {
            CompressionMethod = CompressionMethodValues.Stored;
        }
    }

    internal ZipEntry(string entryName)
    {
        _diskNumberStart = 0;
        _versionMadeByPlatform = CurrentZipPlatform;
        _versionMadeBySpecification = ZipVersionNeededValues.Default;
        _versionToExtract = ZipVersionNeededValues.Default; // this must happen before following two assignment
        _generalPurposeBitFlag = 0;
        CompressionMethod = CompressionMethodValues.Deflate;
        _lastModified = DateTimeOffset.Now;

        _compressedSize = 0; // we don't know these yet
        _uncompressedSize = 0;
        _externalFileAttr = entryName.EndsWith(Path.DirectorySeparatorChar) || entryName.EndsWith(Path.AltDirectorySeparatorChar)
                                    ? ZipArchiveEntryConstants.DefaultDirectoryExternalAttributes
                                    : ZipArchiveEntryConstants.DefaultFileExternalAttributes;

        _offsetOfLocalHeader = 0;
        _storedOffsetOfCompressedData = null;
        _crc32 = 0;

        _compressedBytes = null;
        _storedUncompressedData = null;
        _currentlyOpenForWrite = false;
        _everOpenedForWrite = false;
        _outstandingWriteStream = null;

        FullName = entryName;

        _cdUnknownExtraFields = null;
        _lhUnknownExtraFields = null;

        _fileComment = Array.Empty<byte>();

        _compressionLevel = null;

        if (_storedEntryNameBytes.Length > ushort.MaxValue)
            throw new ArgumentException(SR.EntryNamesTooLong);

    }

    [CLSCompliant(false)]
    public uint Crc32 => _crc32;

    public bool IsEncrypted => _isEncrypted;

    public long CompressedLength
    {
        get
        {
            if (_everOpenedForWrite)
                throw new InvalidOperationException(SR.LengthAfterWrite);
            return _compressedSize;
        }
    }

    public int ExternalAttributes
    {
        get
        {
            return (int)_externalFileAttr;
        }
        set
        {
            _externalFileAttr = (uint)value;
        }
    }

    [AllowNull]
    public string Comment
    {
        get => GetArchiveStreamEntryNameAndCommentEncoding().GetString(_fileComment);
        set
        {
            _fileComment = ZipHelper.GetEncodedTruncatedBytesFromString(value, GetArchiveStreamEntryNameAndCommentEncoding(), ushort.MaxValue, out bool isUTF8);

            if (isUTF8)
            {
                _generalPurposeBitFlag |= BitFlagValues.UnicodeFileNameAndComment;
            }
        }
    }

    /// <summary>
    /// The relative path of the entry as stored in the Zip archive. Note that Zip archives allow any string to be the path of the entry, including invalid and absolute paths.
    /// </summary>
    public string FullName
    {
        get
        {
            return _storedEntryName;
        }

        [MemberNotNull(nameof(_storedEntryNameBytes))]
        [MemberNotNull(nameof(_storedEntryName))]
        private set
        {
            ArgumentNullException.ThrowIfNull(value, nameof(FullName));

            _storedEntryNameBytes = ZipHelper.GetEncodedTruncatedBytesFromString(
                value, GetArchiveStreamEntryNameAndCommentEncoding(), 0 /* No truncation */, out bool isUTF8);

            _storedEntryName = value;

            if (isUTF8)
            {
                _generalPurposeBitFlag |= BitFlagValues.UnicodeFileNameAndComment;
            }
            else
            {
                _generalPurposeBitFlag &= ~BitFlagValues.UnicodeFileNameAndComment;
            }

            DetectEntryNameVersion();
        }
    }
    public DateTimeOffset LastWriteTime
    {
        get
        {
            return _lastModified;
        }
        set
        {
            if (value.DateTime.Year < ZipHelper.ValidZipDate_YearMin || value.DateTime.Year > ZipHelper.ValidZipDate_YearMax)
                throw new ArgumentOutOfRangeException(nameof(value), SR.DateTimeOutOfRange);

            _lastModified = value;
        }
    }

    public long Length
    {
        get
        {
            if (_everOpenedForWrite)
                throw new InvalidOperationException(SR.LengthAfterWrite);
            return _uncompressedSize;
        }
    }
    public string Name => ParseFileName(FullName, _versionMadeByPlatform);

    public Stream? DataStream
    {
        get => null;
        set { throw null!; }
    }

    public void ExtractToFile(string destinationFileName, bool overwrite)
    {

    }

    public override string ToString() => FullName;

    private Encoding GetArchiveStreamEntryNameAndCommentEncoding() => _readerOfOrigin?.EntryNameAndCommentEncoding ?? Encoding.UTF8;

    private long OffsetOfCompressedData
    {
        get
        {
            if (_storedOffsetOfCompressedData == null)
            {
                Debug.Assert(_archive.ArchiveReader != null);
                _archive.ArchiveStream.Seek(_offsetOfLocalHeader, SeekOrigin.Begin);
                // by calling this, we are using local header _storedEntryNameBytes.Length and extraFieldLength
                // to find start of data, but still using central directory size information
                if (!ZipLocalFileHeader.TrySkipBlock(_archive.ArchiveReader))
                    throw new InvalidDataException(SR.LocalFileHeaderCorrupt);
                _storedOffsetOfCompressedData = _archive.ArchiveStream.Position;
            }
            return _storedOffsetOfCompressedData.Value;
        }
    }

    private MemoryStream UncompressedData
    {
        get
        {
            if (_storedUncompressedData == null)
            {
                // this means we have never opened it before

                // if _uncompressedSize > int.MaxValue, it's still okay, because MemoryStream will just
                // grow as data is copied into it
                _storedUncompressedData = new MemoryStream((int)_uncompressedSize);

                if (_originallyInArchive)
                {
                    using (Stream decompressor = OpenInReadMode(false))
                    {
                        try
                        {
                            decompressor.CopyTo(_storedUncompressedData);
                        }
                        catch (InvalidDataException)
                        {
                            // this is the case where the archive say the entry is deflate, but deflateStream
                            // throws an InvalidDataException. This property should only be getting accessed in
                            // Update mode, so we want to make sure _storedUncompressedData stays null so
                            // that later when we dispose the archive, this entry loads the compressedBytes, and
                            // copies them straight over
                            _storedUncompressedData.Dispose();
                            _storedUncompressedData = null;
                            _currentlyOpenForWrite = false;
                            _everOpenedForWrite = false;
                            throw;
                        }
                    }
                }

                // if they start modifying it and the compression method is not "store", we should make sure it will get deflated
                if (CompressionMethod != CompressionMethodValues.Stored)
                {
                    CompressionMethod = CompressionMethodValues.Deflate;
                }
            }

            return _storedUncompressedData;
        }
    }

    private CompressionMethodValues CompressionMethod
    {
        get { return _storedCompressionMethod; }
        set
        {
            if (value == CompressionMethodValues.Deflate)
                VersionToExtractAtLeast(ZipVersionNeededValues.Deflate);
            else if (value == CompressionMethodValues.Deflate64)
                VersionToExtractAtLeast(ZipVersionNeededValues.Deflate64);
            _storedCompressionMethod = value;
        }
    }

    private void VersionToExtractAtLeast(ZipVersionNeededValues value)
    {
        if (_versionToExtract < value)
        {
            _versionToExtract = value;
        }
        if (_versionMadeBySpecification < value)
        {
            _versionMadeBySpecification = value;
        }
    }

    private void DetectEntryNameVersion()
    {
        if (ParseFileName(_storedEntryName, _versionMadeByPlatform) == "")
        {
            VersionToExtractAtLeast(ZipVersionNeededValues.ExplicitDirectory);
        }
    }

    /// <summary>
    /// Gets the file name of the path based on Windows path separator characters
    /// </summary>
    private static string GetFileName_Windows(string path)
    {
        int length = path.Length;
        for (int i = length; --i >= 0;)
        {
            char ch = path[i];
            if (ch == '\\' || ch == '/' || ch == ':')
                return path.Substring(i + 1);
        }
        return path;
    }

    /// <summary>
    /// Gets the file name of the path based on Unix path separator characters
    /// </summary>
    private static string GetFileName_Unix(string path)
    {
        int length = path.Length;
        for (int i = length; --i >= 0;)
            if (path[i] == '/')
                return path.Substring(i + 1);
        return path;
    }

}
