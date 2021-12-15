// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using System.Diagnostics;

namespace System.IO.Compression
{
    public class TarArchive : IDisposable
    {
        internal const short RecordSize = 512;

        internal Stream _archiveStream;
        private Dictionary<int, TarArchiveEntry>? _entries;
        private TarFormat _format;
        private bool _isDisposed;

        internal Dictionary<string, string>? _globalExtendedAttributes;

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
                    throw new ArgumentOutOfRangeException("TarOptions.Mode out of range.", innerException: null); // TODO
            }

            _archiveStream = stream;
            _globalExtendedAttributes = null;
            _format = TarFormat.Unknown;
        }

        public TarArchiveEntry? GetNextEntry()
        {
            ThrowIfDisposed();

            TarArchiveEntry? entry = null;

            if (TryGetNextHeader(out TarHeader header))
            {
                entry = new TarArchiveEntry(this, header);
                OverwriteExtendedAttributesWithGlobalIfNeeded(entry);
                AddEntry(entry);
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

        private bool TryGetNextHeader(out TarHeader header)
        {
            if (TarHeader.TryGetNextHeader(_archiveStream, _format, out header))
            {
                if (header.Format == TarFormat.Pax &&
                    header.TypeFlag == TarHeader.GlobalExtendedAttributesEntryType)
                {
                    // A PAX global extended attributes entry needs to be analyzed for its attributes section,
                    // but we should not return its header; instead, we return the next one.

                    // We should not expect two 'g' entries
                    Debug.Assert(_globalExtendedAttributes == null);

                    // Retrieving the global attributes is all we care about from a 'g' entry.
                    _globalExtendedAttributes = header.ExtendedAttributes;

                    if (TarHeader.TryGetNextHeader(_archiveStream, _format, out header))
                    {
                        UpdateArchiveFormatAndStreamPosition(header);
                        return true;
                    }
                }
                else
                {
                    UpdateArchiveFormatAndStreamPosition(header);
                    return true;
                }
            }
            return false;
        }

        private void UpdateArchiveFormatAndStreamPosition(TarHeader header)
        {
            Debug.Assert(header.Format != TarFormat.Unknown);
            if (_format == TarFormat.Unknown)
            {
                _format = header.Format;
            }
            else if (header.Format != _format)
            {
                throw new FormatException("The archive contains entries in different tar formats."); // TODO
            }
        }

        private void OverwriteExtendedAttributesWithGlobalIfNeeded(TarArchiveEntry entry)
        {
            if (_globalExtendedAttributes != null)
            {
                if (entry._header.ExtendedAttributes == null)
                {
                    entry._header.ExtendedAttributes = _globalExtendedAttributes;
                }
                else
                {
                    foreach ((string key, string value) in _globalExtendedAttributes)
                    {
                        if (!entry._header.ExtendedAttributes.TryAdd(key, value))
                        {
                            entry._header.ExtendedAttributes[key] = value;
                        }
                    }
                }
            }
        }
    }
}
