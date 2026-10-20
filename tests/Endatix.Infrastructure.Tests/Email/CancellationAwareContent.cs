using System.Net;
using System.Text;

namespace Endatix.Infrastructure.Tests.Email;

/// <summary>
/// A provider response body that, like a real network stream, fails to read once its token is cancelled,
/// or always fails to read when <paramref name="resetOnRead"/> is set (a connection reset after the headers).
/// </summary>
internal sealed class CancellationAwareContent(string body, bool resetOnRead = false) : HttpContent
{
    private readonly byte[] _bytes = Encoding.UTF8.GetBytes(body);

    protected override Task SerializeToStreamAsync(Stream stream, TransportContext? context) =>
        SerializeToStreamAsync(stream, context, CancellationToken.None);

    protected override async Task SerializeToStreamAsync(Stream stream, TransportContext? context, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (resetOnRead)
        {
            throw new IOException("Connection reset");
        }

        await stream.WriteAsync(_bytes, cancellationToken);
    }

    protected override bool TryComputeLength(out long length)
    {
        length = _bytes.Length;
        return true;
    }
}
