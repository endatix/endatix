using System.Text.Json;
using Endatix.Core.Abstractions.BackgroundJobs;
using Endatix.Core.Entities;
using Endatix.Core.Infrastructure.Domain;
using Endatix.Core.Infrastructure.Result;
using Endatix.Core.Specifications;
using Endatix.Infrastructure.Identity.Authentication;
using Endatix.Outbox.Engine;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Endatix.Infrastructure.Features.Outbox;

/// <summary>Why an outbox work's job failed for good: no retry can fix either.</summary>
public static class OutboxJobFailures
{
    /// <summary>The outbox row is gone, or belongs to another tenant than the job.</summary>
    /// <remarks>Each attempt re-reads the row, so outbox retention must allow for it: see background-processing.mdx.</remarks>
    public const string MessageGone = "The outbox message no longer exists.";

    /// <summary>The row's payload cannot be read, or names another tenant than the row.</summary>
    public const string MessageUnreadable = "The outbox message could not be read.";

    /// <summary>An app-level row's payload names another tenant than the job's.</summary>
    public const string OtherTenant = "The outbox message belongs to another tenant.";
}

/// <summary>
/// Runs an <see cref="IOutboxWork"/> as a background job: loads the job's outbox message and runs the same work the
/// relay runs inline.
/// </summary>
/// <remarks>
/// A message that is gone or cannot be read is a failure result, so the job ends on its first attempt. The work
/// itself throws on a transient problem, as it does inline, and the job retries.
/// </remarks>
internal sealed class OutboxJobHandler<TPayload, TWork>(
    OutboxJobMessages messages,
    TWork work,
    ILogger<OutboxJobHandler<TPayload, TWork>> logger) : BackgroundJobHandler<TPayload>(logger)
    where TPayload : IOutboxJobPayload<TPayload>
    where TWork : class, IOutboxWork
{
    protected override async Task<Result> ExecuteAsync(
        BackgroundJobContext job,
        TPayload payload,
        CancellationToken cancellationToken)
    {
        var (message, refused) = await messages.FindAsync(job, payload.OutboxMessageId, cancellationToken);
        if (refused is not null)
        {
            return Failure(refused);
        }

        if (!OutboxWorkReader.TryRead(work, message!, out var outboxWork))
        {
            return Failure(OutboxJobFailures.MessageUnreadable);
        }

        await outboxWork.RunAsync(cancellationToken);
        return Result.Success();
    }

    private static Result Failure(string message) => Result.Invalid(new ValidationError(message));
}

/// <summary>Loads the outbox message an outbox work's job processes.</summary>
internal sealed class OutboxJobMessages(
    IRepository<OutboxMessage> outboxMessages,
    IOptionsMonitor<OutboxWorkSettings> settings)
{
    /// <summary>The job's message, or the <see cref="OutboxJobFailures"/> reason the job cannot have it.</summary>
    public async Task<OutboxJobLookup> FindAsync(BackgroundJobContext job, long outboxMessageId, CancellationToken cancellationToken)
    {
        // Scoped to the tenant explicitly: outside a request nothing else scopes this read. An app-level row carries
        // no tenant; its job belongs to the tenant the payload names.
        var readTenant = settings.Get(job.JobType).ReadTenant;
        var row = await outboxMessages.FirstOrDefaultAsync(
            new OutboxMessageByIdForTenantSpec(outboxMessageId, readTenant is null ? job.TenantId : AuthConstants.DEFAULT_TENANT_ID),
            cancellationToken);
        if (row is null)
        {
            return OutboxJobLookup.Refused(OutboxJobFailures.MessageGone);
        }

        var message = new OutboxMessageView(row);
        return readTenant is null ? new OutboxJobLookup(message, null) : ForTenant(readTenant, message, job.TenantId);
    }

    private static OutboxJobLookup ForTenant(Func<IOutboxMessage, long> readTenant, IOutboxMessage message, long tenantId)
    {
        try
        {
            return readTenant(message) == tenantId
                ? new OutboxJobLookup(message, null)
                : OutboxJobLookup.Refused(OutboxJobFailures.OtherTenant);
        }
        catch (Exception exception) when (OutboxWorkReader.IsUnreadable(exception))
        {
            return OutboxJobLookup.Refused(OutboxJobFailures.MessageUnreadable);
        }
    }
}

/// <summary>A job's outbox message, or why the job is refused it; exactly one is set.</summary>
internal readonly record struct OutboxJobLookup(IOutboxMessage? Message, string? Failure)
{
    public static OutboxJobLookup Refused(string failure) => new(null, failure);
}

/// <summary>The one rule for a message an outbox work cannot read: its failure is the message's, not the moment's.</summary>
internal static class OutboxWorkReader
{
    public static bool IsUnreadable(Exception exception) => exception is InvalidOperationException or JsonException;

    public static bool TryRead(IOutboxWork work, IOutboxMessage message, out OutboxWork outboxWork)
    {
        try
        {
            outboxWork = work.Read(message);
            return true;
        }
        catch (Exception exception) when (IsUnreadable(exception))
        {
            outboxWork = OutboxWork.None;
            return false;
        }
    }
}
