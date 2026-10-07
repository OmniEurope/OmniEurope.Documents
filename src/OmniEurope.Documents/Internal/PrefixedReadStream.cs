// SPDX-License-Identifier: EUPL-1.2
namespace OmniEurope.Documents.Internal;

/// <summary>
/// A read-only forward stream that first serves bytes already read from <paramref name="inner"/> (a
/// sniffed prefix), then the rest of <paramref name="inner"/>. Lets a reader detect an encoding on the
/// first bytes of a non-seekable stream without losing them.
/// </summary>
internal sealed class PrefixedReadStream(byte[] prefix, int prefixOffset, int prefixLength, Stream inner, bool leaveOpen)
    : Stream
{
    private int _offset = prefixOffset;
    private readonly int _end = prefixOffset + prefixLength;

    public override bool CanRead => true;

    public override bool CanSeek => false;

    public override bool CanWrite => false;

    public override long Length => throw new NotSupportedException();

    public override long Position
    {
        get => throw new NotSupportedException();
        set => throw new NotSupportedException();
    }

    public override int Read(byte[] buffer, int offset, int count) => Read(buffer.AsSpan(offset, count));

    public override int Read(Span<byte> buffer)
    {
        if (_offset < _end)
        {
            var n = Math.Min(buffer.Length, _end - _offset);
            prefix.AsSpan(_offset, n).CopyTo(buffer);
            _offset += n;
            return n;
        }

        return inner.Read(buffer);
    }

    public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
    {
        if (_offset < _end)
        {
            var n = Math.Min(buffer.Length, _end - _offset);
            prefix.AsMemory(_offset, n).CopyTo(buffer);
            _offset += n;
            return n;
        }

        return await inner.ReadAsync(buffer, cancellationToken).ConfigureAwait(false);
    }

    public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) =>
        ReadAsync(buffer.AsMemory(offset, count), cancellationToken).AsTask();

    public override void Flush()
    {
    }

    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

    public override void SetLength(long value) => throw new NotSupportedException();

    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

    protected override void Dispose(bool disposing)
    {
        if (disposing && !leaveOpen)
        {
            inner.Dispose();
        }

        base.Dispose(disposing);
    }

    /// <summary>Reads up to <paramref name="count"/> bytes, looping until the stream ends.</summary>
    public static int ReadAtMost(Stream stream, byte[] buffer, int count)
    {
        var total = 0;
        while (total < count)
        {
            var n = stream.Read(buffer, total, count - total);
            if (n == 0)
            {
                break;
            }

            total += n;
        }

        return total;
    }

    /// <summary>Asynchronous <see cref="ReadAtMost"/>.</summary>
    public static async Task<int> ReadAtMostAsync(Stream stream, byte[] buffer, int count, CancellationToken cancellationToken)
    {
        var total = 0;
        while (total < count)
        {
            var n = await stream.ReadAsync(buffer.AsMemory(total, count - total), cancellationToken).ConfigureAwait(false);
            if (n == 0)
            {
                break;
            }

            total += n;
        }

        return total;
    }
}
