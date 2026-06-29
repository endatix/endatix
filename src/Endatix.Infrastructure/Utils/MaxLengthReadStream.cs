namespace Endatix.Infrastructure.Utils;

/// <summary>
/// Read-only stream that throws when more than <paramref name="maxLength"/> bytes are read.
/// </summary>
internal sealed class MaxLengthReadStream(Stream inner, long maxLength) : Stream
{
    private long _bytesRead;

    public override bool CanRead => inner.CanRead;

    public override bool CanSeek => false;

    public override bool CanWrite => false;

    public override long Length => throw new NotSupportedException();

    public override long Position
    {
        get => _bytesRead;
        set => throw new NotSupportedException();
    }

    public override void Flush() => inner.Flush();

    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

    public override void SetLength(long value) => throw new NotSupportedException();

    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

    public override int Read(byte[] buffer, int offset, int count)
    {
        var read = inner.Read(buffer, offset, GetAllowedCount(count));
        TrackBytesRead(read);
        return read;
    }

    public override async Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
    {
        var read = await inner.ReadAsync(buffer.AsMemory(offset, GetAllowedCount(count)), cancellationToken);
        TrackBytesRead(read);
        return read;
    }

    public override int Read(Span<byte> buffer)
    {
        var read = inner.Read(buffer[..GetAllowedCount(buffer.Length)]);
        TrackBytesRead(read);
        return read;
    }

    public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
    {
        var read = await inner.ReadAsync(buffer[..GetAllowedCount(buffer.Length)], cancellationToken);
        TrackBytesRead(read);
        return read;
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            inner.Dispose();
        }

        base.Dispose(disposing);
    }

    private int GetAllowedCount(int requested)
    {
        if (_bytesRead >= maxLength)
        {
            ThrowLimitExceeded();
        }

        var remaining = maxLength - _bytesRead;
        return (int)Math.Min(requested, remaining);
    }

    private void TrackBytesRead(int read)
    {
        if (read <= 0)
        {
            return;
        }

        _bytesRead += read;
        if (_bytesRead > maxLength)
        {
            ThrowLimitExceeded();
        }
    }

    private void ThrowLimitExceeded() =>
        throw new InvalidDataException($"Stream exceeded the maximum allowed size of {maxLength} bytes.");
}
