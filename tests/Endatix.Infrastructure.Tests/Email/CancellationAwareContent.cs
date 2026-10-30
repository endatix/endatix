using System.Net;
using System.Text;

namespace Endatix.Infrastructure.Tests.Email;

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
