using System.Collections.Immutable;
using System.Net.Sockets;
using Endatix.Infrastructure.Networking;
using Polly.Retry;
using Polly.Timeout;

namespace Endatix.Infrastructure.Features.WebHooks;

/// <summary>Which outcomes of the inline webhook client's resilience pipeline are retried.</summary>
internal static class WebHookRetryPredicate
{
    private static readonly ImmutableArray<Type> ExceptionsToHandle =
    [
        typeof(TimeoutRejectedException),
        typeof(SocketException),
        typeof(HttpRequestException),
    ];

    /// <summary>A refused endpoint address is never retried: the answer cannot change while the configuration stays.</summary>
    public static ValueTask<bool> ShouldRetry(RetryPredicateArguments<HttpResponseMessage> ex) =>
        new(!OutboundAddressRefusedException.IsCauseOf(ex.Outcome.Exception)
            && (ExceptionsToHandle.Contains(ex.GetType()) || ex.Outcome.Result?.IsSuccessStatusCode == false));
}
