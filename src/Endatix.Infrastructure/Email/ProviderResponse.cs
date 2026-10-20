using System.Net;

namespace Endatix.Infrastructure.Email;

internal static class ProviderResponse
{
    private const string UNAVAILABLE_BODY = "<response body unavailable>";
    private static readonly TimeSpan _bodyReadTimeout = TimeSpan.FromSeconds(5);

    internal static async Task<string> ReadBodyForLogAsync(HttpContent body)
    {
        using var timeout = new CancellationTokenSource(_bodyReadTimeout);
        try
        {
            return await body.ReadAsStringAsync(timeout.Token);
        }
        catch (Exception ex) when (IsReadFailure(ex))
        {
            return UNAVAILABLE_BODY;
        }
    }

    internal static async Task<HttpContent> BufferAsync(HttpContent body)
    {
        using var timeout = new CancellationTokenSource(_bodyReadTimeout);
        try
        {
            await body.LoadIntoBufferAsync(timeout.Token);
            return body;
        }
        catch (Exception ex) when (IsReadFailure(ex))
        {
            body.Dispose();
            return new StringContent(UNAVAILABLE_BODY);
        }
    }

    internal static async Task<string> ReadAcceptedBodyForLogAsync(string provider, HttpStatusCode statusCode, HttpContent body)
    {
        var bodyText = await ReadBodyForLogAsync(body);
        if ((int)statusCode is < 200 or > 299)
        {
            throw new EmailProviderException(provider, statusCode, bodyText);
        }

        return bodyText;
    }

    private static bool IsReadFailure(Exception ex) =>
        ex is OperationCanceledException or HttpRequestException or IOException;
}
