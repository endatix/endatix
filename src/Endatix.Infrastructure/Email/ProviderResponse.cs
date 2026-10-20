namespace Endatix.Infrastructure.Email;

internal static class ProviderResponse
{
    private static readonly TimeSpan _bodyReadTimeout = TimeSpan.FromSeconds(5);

    internal static async Task<string> ReadBodyForLogAsync(HttpContent body)
    {
        using var timeout = new CancellationTokenSource(_bodyReadTimeout);
        try
        {
            return await body.ReadAsStringAsync(timeout.Token);
        }
        catch (Exception ex) when (ex is OperationCanceledException or HttpRequestException or IOException)
        {
            return "<response body unavailable>";
        }
    }
}
