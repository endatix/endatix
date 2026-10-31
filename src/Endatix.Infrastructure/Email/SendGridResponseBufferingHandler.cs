namespace Endatix.Infrastructure.Email;

/// <summary>
/// Reads SendGrid's response body without the caller's token. The SDK sends with
/// <see cref="HttpCompletionOption.ResponseContentRead"/>, so HttpClient would otherwise buffer the
/// body under that token and a late cancellation would fail an email SendGrid already accepted.
/// HttpClient skips its own buffering when the content is already buffered.
/// </summary>
internal sealed class SendGridResponseBufferingHandler : DelegatingHandler
{
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var response = await base.SendAsync(request, cancellationToken);
        response.Content = await ProviderResponse.BufferAsync(response.Content);
        return response;
    }
}
