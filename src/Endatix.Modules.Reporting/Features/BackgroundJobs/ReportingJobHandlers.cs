using Endatix.Core.Abstractions.BackgroundJobs;
using Endatix.Core.Entities;
using Endatix.Core.Infrastructure.Domain;
using Endatix.Core.Infrastructure.Result;
using Endatix.Modules.Reporting.Features.Outbox;
using Endatix.Outbox.Engine;
using Microsoft.Extensions.Logging;

namespace Endatix.Modules.Reporting.Features.BackgroundJobs;

/// <summary>Compiles a form's schema after its definition changed.</summary>
internal sealed class CompileFormSchemaJobHandler(
    IRepository<OutboxMessage> outboxMessages,
    CompileFormSchemaOutboxHandler work) : ReportingOutboxJobHandler<ReportingCompileFormSchemaPayload>(outboxMessages)
{
    protected override async Task<Result> RunAsync(BackgroundJobContext job, IOutboxMessage message, CancellationToken cancellationToken)
    {
        if (!TryParse(() => CompileFormSchemaOutboxHandler.Parse(message), out var input))
        {
            return Failure(MessageUnreadable);
        }

        if (input!.TenantId != job.TenantId)
        {
            return Failure(OtherTenant);
        }

        await work.ProcessAsync(input, cancellationToken);
        return Result.Success();
    }
}

/// <summary>Flattens a completed or updated submission into the Reporting read model.</summary>
internal sealed class FlattenSubmissionJobHandler(
    IRepository<OutboxMessage> outboxMessages,
    FlattenSubmissionOutboxHandler work,
    ILogger<FlattenSubmissionJobHandler> logger) : ReportingOutboxJobHandler<ReportingFlattenSubmissionPayload>(outboxMessages)
{
    protected override async Task<Result> RunAsync(BackgroundJobContext job, IOutboxMessage message, CancellationToken cancellationToken)
    {
        if (!TryParse(() => FlattenSubmissionOutboxHandler.Parse(message, logger), out var input))
        {
            return Failure(MessageUnreadable);
        }

        // A change that does not touch the submission's data has nothing to flatten; the job is done.
        if (input is null)
        {
            return Result.Success();
        }

        if (input.TenantId != job.TenantId)
        {
            return Failure(OtherTenant);
        }

        await work.ProcessAsync(input, cancellationToken);
        return Result.Success();
    }
}

/// <summary>Provisions the default export formats of a new tenant.</summary>
/// <remarks>
/// The outbox row is app-level, so it is read under no tenant, and the job belongs to the tenant being created,
/// which is the tenant the payload names.
/// </remarks>
internal sealed class SeedDefaultExportFormatsJobHandler(
    IRepository<OutboxMessage> outboxMessages,
    SeedDefaultExportFormatsOutboxHandler work) : ReportingOutboxJobHandler<ReportingSeedDefaultExportFormatsPayload>(outboxMessages)
{
    private const long AppLevelTenantId = 0;

    protected override long MessageTenantId(BackgroundJobContext job) => AppLevelTenantId;

    protected override async Task<Result> RunAsync(BackgroundJobContext job, IOutboxMessage message, CancellationToken cancellationToken)
    {
        if (!TryParse(() => SeedDefaultExportFormatsOutboxHandler.Parse(message), out var tenantId))
        {
            return Failure(MessageUnreadable);
        }

        if (tenantId != job.TenantId)
        {
            return Failure(OtherTenant);
        }

        await work.ProcessAsync(tenantId, cancellationToken);
        return Result.Success();
    }
}

/// <summary>Removes a deleted form's Reporting rows.</summary>
internal sealed class SyncFormDeletionJobHandler(
    IRepository<OutboxMessage> outboxMessages,
    SyncFormDeletionOutboxHandler work) : ReportingOutboxJobHandler<ReportingSyncFormDeletionPayload>(outboxMessages)
{
    protected override async Task<Result> RunAsync(BackgroundJobContext job, IOutboxMessage message, CancellationToken cancellationToken)
    {
        if (!TryParse(() => SyncFormDeletionOutboxHandler.Parse(message), out var input))
        {
            return Failure(MessageUnreadable);
        }

        if (input!.TenantId != job.TenantId)
        {
            return Failure(OtherTenant);
        }

        await work.ProcessAsync(input, message.Id, cancellationToken);
        return Result.Success();
    }
}

/// <summary>Removes a deleted submission's flattened row.</summary>
internal sealed class SyncSubmissionDeletionJobHandler(
    IRepository<OutboxMessage> outboxMessages,
    SyncSubmissionDeletionOutboxHandler work) : ReportingOutboxJobHandler<ReportingSyncSubmissionDeletionPayload>(outboxMessages)
{
    protected override async Task<Result> RunAsync(BackgroundJobContext job, IOutboxMessage message, CancellationToken cancellationToken)
    {
        if (!TryParse(() => SyncSubmissionDeletionOutboxHandler.Parse(message), out var input))
        {
            return Failure(MessageUnreadable);
        }

        if (input!.TenantId != job.TenantId)
        {
            return Failure(OtherTenant);
        }

        await work.ProcessAsync(input, message.Id, cancellationToken);
        return Result.Success();
    }
}
