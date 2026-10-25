// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

using System.Security.Cryptography;

namespace EchoPhase.Files
{
    /// <summary>
    /// Reads an upload once: the head already read, then the rest of the
    /// source, counting the bytes and hashing them, and fails past the limit
    /// instead of storing an oversized file.
    /// </summary>
    internal sealed class MeasuringStream : Stream
    {
        private readonly ReadOnlyMemory<byte> _head;
        private readonly Stream _rest;
        private readonly long _limit;
        private readonly IncrementalHash _hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        private int _headRead;

        public MeasuringStream(ReadOnlyMemory<byte> head, Stream rest, long limit)
        {
            _head = head;
            _rest = rest;
            _limit = limit;
        }

        /// <summary>Bytes read so far.</summary>
        public long Count { get; private set; }

        /// <summary>Hex SHA-256 of everything read; read only at the end.</summary>
        public string Sha256 => Convert.ToHexStringLower(_hash.GetCurrentHash());

        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException();
        public override long Position { get => Count; set => throw new NotSupportedException(); }

        public override int Read(byte[] buffer, int offset, int count) => Read(buffer.AsSpan(offset, count));

        public override int Read(Span<byte> buffer)
        {
            int read;
            if (_headRead < _head.Length)
            {
                read = Math.Min(buffer.Length, _head.Length - _headRead);
                _head.Span.Slice(_headRead, read).CopyTo(buffer);
                _headRead += read;
            }
            else
            {
                read = _rest.Read(buffer);
            }
            return Measure(buffer[..read]);
        }

        public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            int read;
            if (_headRead < _head.Length)
            {
                read = Math.Min(buffer.Length, _head.Length - _headRead);
                _head.Slice(_headRead, read).CopyTo(buffer);
                _headRead += read;
            }
            else
            {
                read = await _rest.ReadAsync(buffer, cancellationToken);
            }
            return Measure(buffer.Span[..read]);
        }

        public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) =>
            ReadAsync(buffer.AsMemory(offset, count), cancellationToken).AsTask();

        private int Measure(ReadOnlySpan<byte> read)
        {
            Count += read.Length;
            if (Count > _limit)
                throw new FileTooLargeException(_limit);
            _hash.AppendData(read);
            return read.Length;
        }

        public override void Flush() { }
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

        protected override void Dispose(bool disposing)
        {
            if (disposing)
                _hash.Dispose();
            base.Dispose(disposing);
        }
    }

    /// <summary>An upload went past its limit while being read.</summary>
    internal sealed class FileTooLargeException : IOException
    {
        public FileTooLargeException(long limit)
            : base($"The file is larger than {limit} bytes.")
        {
            Limit = limit;
        }

        public long Limit { get; }
    }
}
