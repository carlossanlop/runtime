// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Text;

namespace System.IO.Compression
{
    public class TarArchive : IDisposable
    {
        internal const short RecordSize = 512;

        internal Stream _archiveStream;
        private TarOptions _options;
        private long _currentHeaderBegin;
        private BinaryReader _reader;
        private bool _isDisposed;

        private Dictionary<int, TarArchiveEntry>? _entries;

        public TarOptions Options => _options;

        public TarArchive(Stream stream, TarOptions? options)
        {
            _options = options ?? new TarOptions();

            switch (_options.Mode)
            {
                case TarArchiveMode.Read:
                    if (!stream.CanRead)
                    {
                        throw new ArgumentException(SR.ReadModeCapabilities);
                    }
                    break;
                default:
                    throw new ArgumentOutOfRangeException("Unsupported tar archive mode.", innerException: null);
            }

            _archiveStream = stream;
            _currentHeaderBegin = 0;
            _reader = new BinaryReader(_archiveStream);
        }

        public TarArchiveEntry? GetNextEntry()
        {
            ThrowIfDisposed();

            if (_reader.PeekChar() == -1)
            {
                return null;
            }

            TarArchiveEntry? entry = null;

            try
            {

                TarHeader header = TarHeader.GetNextHeader(_reader);
                if (header.Checksum != 0)
                {
                    entry = new TarArchiveEntry(this, header);
                    AddEntry(entry);
                }
            }
            finally
            {
                _currentHeaderBegin = _archiveStream.Length;
            }

            return entry;
        }

        public void Dispose()
        {
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!_isDisposed)
            {
                if (disposing)
                {
                    _reader.Dispose();
                    _archiveStream.Dispose();
                }

                _isDisposed = true;
            }
        }

        private void AddEntry(TarArchiveEntry entry)
        {
            ThrowIfDisposed();
            (_entries ??= new()).Add(entry.GetHashCode(), entry);
        }

        internal void ThrowIfDisposed()
        {
            if (_isDisposed)
            {
                throw new ObjectDisposedException(GetType().ToString());
            }
        }
    }
}
