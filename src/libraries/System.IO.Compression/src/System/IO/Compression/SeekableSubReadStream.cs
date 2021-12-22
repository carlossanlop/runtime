// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

namespace System.IO.Compression
{
    internal sealed class SeekableSubReadStream : Stream
    {
        private readonly long _startInSuperStream;
        private long _positionInSuperStream;
        private readonly long _endInSuperStream;
        private readonly Stream _superStream;
        private bool _canRead;
        private bool _isDisposed;

        public SeekableSubReadStream(Stream superStream, long startPosition, long maxLength)
        {
            Debug.Assert(superStream.CanSeek);

            _startInSuperStream = startPosition;
            _positionInSuperStream = startPosition;
            _endInSuperStream = startPosition + maxLength;
            _superStream = superStream;
            _canRead = true;
            _isDisposed = false;
        }

        public override long Length
        {
            get
            {
                ThrowIfDisposed();
                return _endInSuperStream - _startInSuperStream;
            }
        }

        public override long Position
        {
            get
            {
                ThrowIfDisposed();
                return _positionInSuperStream - _startInSuperStream;
            }
            set
            {
                ThrowIfDisposed();
                throw new NotSupportedException(SR.SeekingNotSupported);
            }
        }

        public override bool CanRead => _superStream.CanRead && _canRead;

        public override bool CanSeek => true;

        public override bool CanWrite => false;

        private void ThrowIfDisposed()
        {
            if (_isDisposed)
            {
                throw new ObjectDisposedException(GetType().ToString(), SR.HiddenStreamName);
            }
        }

        private void ThrowIfCantRead()
        {
            if (!CanRead)
            {
                throw new NotSupportedException(SR.ReadingNotSupported);
            }
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            // parameter validation sent to _superStream.Read
            int origCount = count;

            ThrowIfDisposed();
            ThrowIfCantRead();

            if (_superStream.Position != _positionInSuperStream)
            {
                _superStream.Seek(_positionInSuperStream, SeekOrigin.Begin);
            }

            if (_positionInSuperStream + count > _endInSuperStream)
            {
                count = (int)(_endInSuperStream - _positionInSuperStream);
            }

            Debug.Assert(count >= 0);
            Debug.Assert(count <= origCount);

            int ret = _superStream.Read(buffer, offset, count);

            _positionInSuperStream += ret;
            return ret;
        }

        public override int Read(Span<byte> destination)
        {
            // parameter validation sent to _superStream.Read
            int origCount = destination.Length;
            int count = destination.Length;

            ThrowIfDisposed();
            ThrowIfCantRead();

            if (_superStream.Position != _positionInSuperStream)
            {
                _superStream.Seek(_positionInSuperStream, SeekOrigin.Begin);
            }

            if (_positionInSuperStream + count > _endInSuperStream)
            {
                count = (int)(_endInSuperStream - _positionInSuperStream);
            }

            Debug.Assert(count >= 0);
            Debug.Assert(count <= origCount);

            int ret = _superStream.Read(destination.Slice(0, count));

            _positionInSuperStream += ret;
            return ret;
        }

        public override int ReadByte()
        {
            byte b = default;
            return Read(MemoryMarshal.CreateSpan(ref b, 1)) == 1 ? b : -1;
        }

        public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            ValidateBufferArguments(buffer, offset, count);
            return ReadAsync(new Memory<byte>(buffer, offset, count), cancellationToken).AsTask();
        }

        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();
            ThrowIfCantRead();
            return Core(buffer, cancellationToken);

            async ValueTask<int> Core(Memory<byte> buffer, CancellationToken cancellationToken)
            {
                if (_superStream.Position != _positionInSuperStream)
                {
                    _superStream.Seek(_positionInSuperStream, SeekOrigin.Begin);
                }

                if (_positionInSuperStream > _endInSuperStream - buffer.Length)
                {
                    buffer = buffer.Slice(0, (int)(_endInSuperStream - _positionInSuperStream));
                }

                int ret = await _superStream.ReadAsync(buffer, cancellationToken).ConfigureAwait(false);

                _positionInSuperStream += ret;
                return ret;
            }
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            ThrowIfDisposed();
            var newPosition = origin switch
            {
                SeekOrigin.Begin   => _startInSuperStream + offset,
                SeekOrigin.Current => _positionInSuperStream + offset,
                SeekOrigin.End     => _endInSuperStream + offset,
                _ => throw new ArgumentOutOfRangeException(nameof(origin)),
            };
            if (newPosition < _startInSuperStream || newPosition > _endInSuperStream)
            {
                throw new IndexOutOfRangeException(nameof(offset));
            }

            _superStream.Position = newPosition;

            return _superStream.Position;
        }

        public override void SetLength(long value)
        {
            ThrowIfDisposed();
            throw new NotSupportedException(SR.SetLengthRequiresSeekingAndWriting);
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            ThrowIfDisposed();
            throw new NotSupportedException(SR.WritingNotSupported);
        }

        public override void Flush()
        {
            ThrowIfDisposed();
            throw new NotSupportedException(SR.WritingNotSupported);
        }

        // Close the stream for reading.  Note that this does NOT close the superStream (since
        // the substream is just 'a chunk' of the super-stream
        protected override void Dispose(bool disposing)
        {
            if (disposing && !_isDisposed)
            {
                _canRead = false;
                _isDisposed = true;
            }
            base.Dispose(disposing);
        }
    }
}