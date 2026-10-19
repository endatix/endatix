using System.Text.Json;
using Endatix.Infrastructure.Features.Outbox;
using Endatix.Modules.Reporting.Features.SearchCopy;
using Endatix.Outbox.Engine;

namespace Endatix.Modules.Reporting.Features.Outbox;

/// <summary>
/// Rebuilds a form's search copies after a person added a search key (<see cref="SearchCopyRebuildRequests"/>).
/// </summary>
/// <remarks>
/// The same work runs inline in the relay or as a background job; both read the message with <see cref="Parse"/>
/// and do the work with <see cref="ProcessAsync"/>. A rebuild cut off part way throws or stops, and the retry
/// rewrites only the rows not yet stamped.
/// </remarks>
internal sealed class RebuildSearchCopyOutboxHandler(SearchCopyRebuilder rebuilder) : IOutboxIntegrationEventHandler
{
    public static readonly IReadOnlyCollection<string> HandledEventTypes = [SearchCopyRebuildRequests.EventTypeName];

    public IReadOnlyCollection<string> EventTypes => HandledEventTypes;

    /// <inheritdoc />
    public Task HandleAsync(IOutboxMessage message, CancellationToken cancellationToken) =>
        ProcessAsync(Parse(message), cancellationToken);

    /// <summary>Reads the work from the message; throws <see cref="InvalidOperationException"/> when it cannot.</summary>
    public static Input Parse(IOutboxMessage message)
    {
        using var document = JsonDocument.Parse(message.Payload);
        var payload = document.RootElement;

        return new Input(message.GetRequiredTenantId(payload), message.GetRequiredIdProp(payload, "formId"));
    }

    public Task<int> ProcessAsync(Input input, CancellationToken cancellationToken) =>
        rebuilder.RebuildFormAsync(input.TenantId, input.FormId, cancellationToken);

    public sealed record Input(long TenantId, long FormId);
}
