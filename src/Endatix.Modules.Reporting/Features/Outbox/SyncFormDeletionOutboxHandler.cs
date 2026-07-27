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
    /// <inheritdoc />
    public IReadOnlyCollection<string> EventTypes { get; } = [FormDeletedEvent.EventTypeName];

    /// <inheritdoc />
    public async Task HandleAsync(IOutboxMessage message, CancellationToken cancellationToken)
    {
        using var document = JsonDocument.Parse(message.Payload);
        var payload = document.RootElement;

        var tenantId = message.GetRequiredTenantId(payload);
        var formId = message.GetRequiredIdProp(payload, "formId");

        await unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            var schemasDeleted = await formSchemaRepository.DeleteByFormIdAsync(
                tenantId,
                formId,
                cancellationToken);
            var flattenedDeleted = await flattenedSubmissionRepository.DeleteByFormIdAsync(
                tenantId,
                formId,
                cancellationToken);

            await unitOfWork.CommitTransactionAsync(cancellationToken);

            logger.LogInformation(
                "Cleaned reporting rows for form {FormId} (schemasDeleted={SchemasDeleted}, flattenedDeleted={FlattenedDeleted}, outboxMessageId={OutboxMessageId})",
                formId,
                schemasDeleted,
                flattenedDeleted,
                message.Id);
        }
        catch
        {
            await unitOfWork.RollbackTransactionAsync(cancellationToken);
            throw;
        }
    }
}
