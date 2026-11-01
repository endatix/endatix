using System.Diagnostics;
using System.Globalization;
using System.Text.Json;
using Endatix.Core.Abstractions;
using Endatix.Core.Entities;
using Endatix.Core.Infrastructure.Domain;
using Microsoft.Extensions.Logging;

namespace Endatix.Modules.Reporting.Features.SearchCopy;

/// <summary>
/// Asks for a form's search copies to be rebuilt in the background, through the outbox: the relay runs
/// <see cref="Outbox.RebuildSearchCopyOutboxHandler"/> inline, or hands it to a job when the outbox delivers to the job
/// queue.
/// </summary>
/// <remarks>
/// The outbox row lives in the App database, so it cannot commit with the search keys in the Reporting one. It is
/// written after the keys commit, so the request is best-effort: a request that is lost or fails leaves the rows
/// pending, never wrong, and adding the key again, or the next compile, catches them up. The rebuild skips rows already
/// stamped with the current revision, so a duplicate request costs one scan.
/// </remarks>
internal sealed class SearchCopyRebuildRequests(
    IRepository<OutboxMessage> outboxMessages,
    IDateTimeProvider dateTimeProvider,
    ILogger<SearchCopyRebuildRequests> logger)
{
    public const string EventTypeName = "reporting.search_copy.rebuild_requested";

    /// <summary>
    /// Writes the request. It follows a change that has already committed, so the caller going away does not cancel it,
    /// and a failure is logged, not thrown: the change stands and its rows stay pending.
    /// </summary>
    public async Task RequestAsync(long tenantId, long formId)
    {
        try
        {
            await outboxMessages.AddAsync(Message(tenantId, formId), CancellationToken.None);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Could not request a search copy rebuild for form {FormId} of tenant {TenantId}; its rows stay pending", formId, tenantId);
        }
    }

    private OutboxMessage Message(long tenantId, long formId)
    {
        var payload = JsonSerializer.Serialize(new
        {
            tenantId = tenantId.ToString(CultureInfo.InvariantCulture),
            formId = formId.ToString(CultureInfo.InvariantCulture),
        });
        return new OutboxMessage(
            EventTypeName,
            payload,
            tenantId,
            dateTimeProvider.UtcNow.UtcDateTime,
            schemaVersion: 1,
            Activity.Current?.TraceId.ToString());
    }
}
