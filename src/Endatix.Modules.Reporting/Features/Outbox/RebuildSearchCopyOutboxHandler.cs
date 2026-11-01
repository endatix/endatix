using System.Text.Json;
using Endatix.Infrastructure.Features.Outbox;
using Endatix.Modules.Reporting.Features.SearchCopy;
using Endatix.Outbox.Engine;

namespace Endatix.Modules.Reporting.Features.Outbox;

/// <summary>
/// Rebuilds a form's search copies after a person added a search key (<see cref="SearchCopyRebuildRequests"/>).
/// </summary>
/// <remarks>
/// The same work runs inline in the relay or as a background job; both run the work <see cref="Read"/> returns. A
/// rebuild cut off part way throws or stops, and the retry rewrites only the rows not yet stamped.
/// </remarks>
internal sealed class RebuildSearchCopyOutboxHandler(SearchCopyRebuilder rebuilder)
    : IOutboxWork<RebuildSearchCopyOutboxHandler>
{
    public static string JobType => "ReportingRebuildSearchCopy";

    public static IReadOnlyCollection<string> HandledEventTypes { get; } = [SearchCopyRebuildRequests.EventTypeName];

    /// <inheritdoc />
    public OutboxWork Read(IOutboxMessage message) =>
        OutboxWork.For(Parse(message), ProcessAsync);

    /// <summary>Reads the work from the message; throws <see cref="InvalidOperationException"/> when it cannot.</summary>
    public static Input Parse(IOutboxMessage message)
    {
        using var document = JsonDocument.Parse(message.Payload);
        var payload = document.RootElement;

        return new Input(message.GetRequiredTenantId(payload), message.GetRequiredIdProp(payload, "formId"));
    }

    public Task ProcessAsync(Input input, CancellationToken cancellationToken) =>
        rebuilder.RebuildFormAsync(input.TenantId, input.FormId, cancellationToken);

    public sealed record Input(long TenantId, long FormId);
}
