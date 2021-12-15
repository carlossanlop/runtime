// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;

namespace System.IO.Compression
{
    public class TarArchiveEntry
    {
        private TarArchive? _archive;
        internal TarHeader _header;
        internal MemoryStream? _stream;

        public int Checksum => _header.Checksum;
        public int DevMajor => _header.DevMajor;
        public int DevMinor => _header.DevMinor;
        public IReadOnlyDictionary<string, string>? ExtendedAttributes => _header.ExtendedAttributes;
        public int Gid => _header.Gid;
        public string? GName => _header.GName;
        public long Length => _stream != null ? _stream.Length : _header.Size;
        public string LinkName => _header.LinkName;
        public int Mode => _header.Mode;
        public string Name => _header.Name;
        public TarArchiveEntryType TypeFlag => _header.TypeFlag;
        public int Uid => _header.Uid;
        public string? UName => _header.UName;

        internal TarArchiveEntry(TarArchive archive, TarHeader header)
        {
            _archive = archive;
            _header = header;
            _stream = null;
        }

        public Stream? Open()
        {
            ThrowIfInvalidArchive();

            switch (_archive.Options.Mode)
            {
                case TarArchiveMode.Read:
                    return OpenInReadMode();
                default:
                    throw new NotImplementedException("Mode not implemented"); // TODO
            }
        }

        public override string ToString() => Name;

        [MemberNotNull(nameof(_archive))]
        private void ThrowIfInvalidArchive()
        {
            if (_archive == null)
            {
                throw new InvalidOperationException(SR.DeletedEntry);
            }

            _archive.ThrowIfDisposed();
        }

        private Stream? OpenInReadMode()
        {
            ThrowIfInvalidArchive();

            if (!_archive._archiveStream.CanRead)
            {
                throw new InvalidDataException(SR.NotSupported_UnreadableStream);
            }

            switch (TypeFlag)
            {
                case TarArchiveEntryType.OldNormal:
                case TarArchiveEntryType.Normal:
                    {
                        Debug.Assert(_header._dataStream != null);
                        Debug.Assert(_header._dataStream.CanSeek);

                        _header._dataStream.Seek(0, SeekOrigin.Begin);
                        return _header._dataStream;
                    }
                default:
                    break;
            }

            return null;
        }
    }
}
