using System.Text.Json;
using Endatix.Core.Events;
using Endatix.Infrastructure.Features.Outbox;
using Endatix.Modules.Reporting.Data;
using Endatix.Outbox.Engine;
using Microsoft.Extensions.Logging;

namespace Endatix.Modules.Reporting.Features.Outbox;

/// <summary>
/// Hard-deletes form-scoped Reporting rows when a form is deleted.
/// </summary>
internal sealed class SyncFormDeletionOutboxHandler(
    IFormSchemaRepository formSchemaRepository,
    IFlattenedSubmissionRepository flattenedSubmissionRepository,
    IReportingUnitOfWork unitOfWork,
    ILogger<SyncFormDeletionOutboxHandler> logger) : IOutboxIntegrationEventHandler
{
    public static readonly IReadOnlyCollection<string> HandledEventTypes = [FormDeletedEvent.EventTypeName];

    /// <inheritdoc />
    public IReadOnlyCollection<string> EventTypes => HandledEventTypes;

    /// <inheritdoc />
    public Task HandleAsync(IOutboxMessage message, CancellationToken cancellationToken) =>
        ProcessAsync(Parse(message), message.Id, cancellationToken);

    /// <summary>Reads the work from the message; throws <see cref="InvalidOperationException"/> when it cannot.</summary>
    public static Input Parse(IOutboxMessage message)
    {
        using var document = JsonDocument.Parse(message.Payload);
        var payload = document.RootElement;

        return new Input(message.GetRequiredTenantId(payload), message.GetRequiredIdProp(payload, "formId"));
    }

    public async Task ProcessAsync(Input input, long outboxMessageId, CancellationToken cancellationToken)
    {
        var (tenantId, formId) = input;
        var (schemasDeleted, flattenedDeleted) = await unitOfWork.InTransactionAsync(
            async () => (
                await formSchemaRepository.DeleteByFormIdAsync(tenantId, formId, cancellationToken),
                await flattenedSubmissionRepository.DeleteByFormIdAsync(tenantId, formId, cancellationToken)),
            cancellationToken);

        logger.LogInformation(
            "Cleaned reporting rows for form {FormId} (schemasDeleted={SchemasDeleted}, flattenedDeleted={FlattenedDeleted}, outboxMessageId={OutboxMessageId})",
            formId,
            schemasDeleted,
            flattenedDeleted,
            outboxMessageId);
    }

    public sealed record Input(long TenantId, long FormId);
}
