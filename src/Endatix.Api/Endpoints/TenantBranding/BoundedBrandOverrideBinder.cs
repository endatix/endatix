using System.Text;
using Endatix.Core.UseCases.TenantBranding.Publishing;
using FastEndpoints;
using Microsoft.AspNetCore.Http;

namespace Endatix.Api.Endpoints.TenantBranding;

/// <summary>
/// Reads the publish body as text, but never more than one character past the override's bound, so a large body,
/// chunked or not, costs a few kilobytes and comes back as <c>too_large</c> from the command instead of being read
/// whole.
/// </summary>
/// <remarks>
/// A body over the server's own limit (Kestrel's 30 MB) throws on the first read; that is a body over the bound too.
/// The rest of an oversized body is left unread, for the server to drain or drop.
/// </remarks>
internal sealed class BoundedBrandOverrideBinder : IRequestBinder<PublishTenantBrandingRequest>
{
    private const int READ_LIMIT = PublishTenantBrandingCommand.MAX_OVERRIDE_LENGTH + 1;

    /// <inheritdoc/>
    public async ValueTask<PublishTenantBrandingRequest> BindAsync(BinderContext ctx, CancellationToken ct)
    {
        try
        {
            return new PublishTenantBrandingRequest { Content = await ReadAsync(ctx.HttpContext.Request.Body, ct) };
        }
        catch (BadHttpRequestException exception) when (exception.StatusCode == StatusCodes.Status413PayloadTooLarge)
        {
            return new PublishTenantBrandingRequest { Content = new string(' ', READ_LIMIT) };
        }
    }

    private static async Task<string> ReadAsync(Stream body, CancellationToken ct)
    {
        using var reader = new StreamReader(body, Encoding.UTF8, detectEncodingFromByteOrderMarks: false, leaveOpen: true);
        var buffer = new char[READ_LIMIT];
        var length = 0;
        while (length < READ_LIMIT)
        {
            var read = await reader.ReadAsync(buffer.AsMemory(length), ct);
            if (read == 0)
            {
                break;
            }

            length += read;
        }

        return new string(buffer, 0, length);
    }
}
