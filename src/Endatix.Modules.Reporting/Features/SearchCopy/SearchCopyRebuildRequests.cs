using System.Diagnostics;
using System.Globalization;
using System.Text.Json;
using Endatix.Core.Abstractions;
using Endatix.Core.Entities;
using Endatix.Core.Infrastructure.Domain;

namespace Endatix.Modules.Reporting.Features.SearchCopy;

/// <summary>
/// Asks for a form's search copies to be rebuilt in the background, through the outbox: the relay runs
/// <see cref="Outbox.RebuildSearchCopyOutboxHandler"/> inline, or hands it to a job when the outbox delivers to the job
/// queue.
/// </summary>
/// <remarks>
/// The outbox row lives in the App database, so it cannot commit with the search keys in the Reporting one. It is
/// written after the keys commit: a request lost in between leaves the rows pending, never wrong, and adding the key
/// again writes another. The rebuild skips rows already stamped with the current revision, so a duplicate request
/// costs one scan.
/// </remarks>
internal sealed class SearchCopyRebuildRequests(IRepository<OutboxMessage> outboxMessages, IDateTimeProvider dateTimeProvider)
{
    public const string EventTypeName = "reporting.search_copy.rebuild_requested";

    public async Task RequestAsync(long tenantId, long formId, CancellationToken cancellationToken)
    {
        var payload = JsonSerializer.Serialize(new
        {
            tenantId = tenantId.ToString(CultureInfo.InvariantCulture),
            formId = formId.ToString(CultureInfo.InvariantCulture),
        });
        OutboxMessage message = new(
            EventTypeName,
            payload,
            tenantId,
            dateTimeProvider.UtcNow.UtcDateTime,
            schemaVersion: 1,
            Activity.Current?.TraceId.ToString());
        await outboxMessages.AddAsync(message, cancellationToken);
    }
}
