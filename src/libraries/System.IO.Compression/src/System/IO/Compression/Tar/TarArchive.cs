// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;

namespace System.IO.Compression
{
    public class TarArchive : IDisposable
    {
        internal const short RecordSize = 512;

        internal Stream _archiveStream;
        private Dictionary<int, TarArchiveEntry>? _entries;
        private TarFormat _format;
        private bool _isDisposed;
        private long _lastDataStartPosition;

        public TarFormat Format => _format;

        public TarOptions Options { get; }

        public TarArchive(Stream stream, TarOptions? options)
        {
            if (stream == null)
            {
                throw new ArgumentNullException(nameof(stream));
            }

            Options = options ?? new TarOptions();

            switch (Options.Mode)
            {
                case TarArchiveMode.Read:
                    if (!stream.CanRead)
                    {
                        throw new ArgumentException(SR.ReadModeCapabilities);
                    }
                    break;
                default:
                    throw new ArgumentOutOfRangeException("TarOptions.Mode out of range.", innerException: null);
            }

            _archiveStream = stream;
            _lastDataStartPosition = 0;

            // The real format is set after reading the first entry
            _format = TarFormat.Unknown;
        }

        public TarArchiveEntry? GetNextEntry()
        {
            ThrowIfDisposed();

            TarArchiveEntry? entry = null;

            if (TarHeader.TryGetNextHeader(_archiveStream, _lastDataStartPosition, out TarHeader header))
            {
                entry = new TarArchiveEntry(this, header);
                AddEntry(entry);
                _lastDataStartPosition = header.DataStartPosition;

                if (_format == TarFormat.Unknown)
                {
                    _format = header.Format;
                }
                else if (header.Format != _format)
                {
                    throw new FormatException("The archive contains entries in different tar formats.");
                }
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
                if (disposing && !Options.LeaveOpen)
                {
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
