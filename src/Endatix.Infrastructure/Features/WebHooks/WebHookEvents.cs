using Endatix.Core.Features.WebHooks;
using Endatix.Infrastructure.Utils;

namespace Endatix.Infrastructure.Features.WebHooks;

/// <summary>
/// The outbox event types that are delivered to webhooks, and the webhook operation each one is.
/// </summary>
internal static class WebHookEvents
{
    public static readonly IReadOnlyDictionary<string, WebHookOperation> OperationsByEventType =
        new[]
        {
            WebHookOperation.FormCreated,
            WebHookOperation.FormUpdated,
            WebHookOperation.FormEnabledStateChanged,
            WebHookOperation.SubmissionCompleted,
            WebHookOperation.SubmissionCollectionStatusChanged,
            WebHookOperation.FormDeleted,
        }.ToDictionary(operation => StringUtils.ToDottedEventType(operation.EventName), StringComparer.Ordinal);
}
