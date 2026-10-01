using Endatix.Core.Abstractions.BackgroundJobs;

namespace Endatix.Infrastructure.Features.BackgroundJobs.Handlers;

/// <summary>
/// One webhook delivery: which outbox message, to which configured endpoint.
/// </summary>
/// <remarks>
/// Deliberately thin. The event payload, the endpoint's URL and its credentials are read when the job runs, so
/// none of them is copied into the job row — a URL can carry a token in its query string.
/// </remarks>
/// <param name="OutboxMessageId">The outbox message whose event is delivered.</param>
/// <param name="EndpointKey">The endpoint, by <see cref="WebHooks.WebHookEndpointKey"/>.</param>
public sealed record WebHookDeliveryPayload(long OutboxMessageId, string EndpointKey) : IBackgroundJobPayload
{
    public static string JobType => "WebHookDelivery";
}
