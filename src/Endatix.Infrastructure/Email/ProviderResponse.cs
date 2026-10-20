namespace Endatix.Infrastructure.Email;

/// <summary>
/// Reads an email provider's response body for the log.
/// </summary>
internal static class ProviderResponse
{
    private static readonly TimeSpan _bodyReadTimeout = TimeSpan.FromSeconds(5);

    /// <summary>
    /// The provider has already answered, so the send is done whatever happens here. The caller's token is not
    /// used (a late cancellation must not fail the send and trigger a retry), the read is bounded, and a failed
    /// read is logged as a placeholder instead of failing the send.
    /// </summary>
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
