using Endatix.Infrastructure.Utils;

namespace Endatix.Infrastructure.Features.Submissions;

/// <summary>
/// Owns the HTTP response and exposes its content through a byte-limited read stream.
/// </summary>
internal sealed class BoundedHttpResponseStream : Stream
{
    private readonly HttpResponseMessage _response;
    private readonly MaxLengthReadStream _content;

    public BoundedHttpResponseStream(HttpResponseMessage response, long maxBytes)
    {
        _response = response;
        var inner = response.Content.ReadAsStream();
        _content = new MaxLengthReadStream(inner, maxBytes);
    }

    public override bool CanRead => _content.CanRead;

    public override bool CanSeek => false;

    public override bool CanWrite => false;

    public override long Length => throw new NotSupportedException();

    public override long Position
    {
        get => _content.Position;
        set => throw new NotSupportedException();
    }

    public override void Flush() => _content.Flush();

    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

    public override void SetLength(long value) => throw new NotSupportedException();

    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

    public override int Read(byte[] buffer, int offset, int count) => _content.Read(buffer, offset, count);

    public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) =>
        _content.ReadAsync(buffer, offset, count, cancellationToken);

    public override int Read(Span<byte> buffer) => _content.Read(buffer);

    public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default) =>
        _content.ReadAsync(buffer, cancellationToken);

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _content.Dispose();
            _response.Dispose();
        }

        base.Dispose(disposing);
    }
}
