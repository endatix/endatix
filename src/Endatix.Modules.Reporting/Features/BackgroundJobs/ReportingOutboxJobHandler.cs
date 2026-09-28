using Endatix.Core.Abstractions.BackgroundJobs;
using Endatix.Core.Entities;
using Endatix.Core.Infrastructure.Domain;
using Endatix.Core.Infrastructure.Result;
using Endatix.Core.Specifications;
using Endatix.Infrastructure.Features.Outbox;
using Endatix.Outbox.Engine;

namespace Endatix.Modules.Reporting.Features.BackgroundJobs;

/// <summary>
/// A Reporting outbox handler's work, run as a background job: the job loads its outbox message and hands it to
/// the same code the relay runs inline.
/// </summary>
/// <remarks>
/// A message that is gone or cannot be read, or that names another tenant, is a failure no retry can fix. The
/// work itself throws on a transient problem, as it always has, and the job retries.
/// </remarks>
internal abstract class ReportingOutboxJobHandler<TPayload>(IRepository<OutboxMessage> outboxMessages)
    : BackgroundJobHandler<TPayload>
    where TPayload : IReportingOutboxJobPayload
{
    public const string MessageGone = "The outbox message no longer exists.";
    public const string MessageUnreadable = "The outbox message could not be read.";
    public const string OtherTenant = "The outbox message belongs to another tenant.";

    /// <summary>
    /// The tenant the outbox row itself carries. The job's own by default; an app-level message carries none.
    /// </summary>
    protected virtual long MessageTenantId(BackgroundJobContext job) => job.TenantId;

    protected sealed override async Task<Result> ExecuteAsync(
        BackgroundJobContext job,
        TPayload payload,
        CancellationToken cancellationToken)
    {
        // Scoped to the tenant explicitly: outside a request nothing else scopes this read.
        var message = await outboxMessages.FirstOrDefaultAsync(
            new OutboxMessageByIdForTenantSpec(payload.OutboxMessageId, MessageTenantId(job)),
            cancellationToken);

        return message is null
            ? Failure(MessageGone)
            : await RunAsync(job, new OutboxMessageView(message), cancellationToken);
    }

    /// <summary>Reads the message and does the work, or returns why it cannot.</summary>
    protected abstract Task<Result> RunAsync(
        BackgroundJobContext job,
        IOutboxMessage message,
        CancellationToken cancellationToken);

    protected static Result Failure(string message) => Result.Invalid(new ValidationError(message));

    /// <summary>
    /// Reads the message with the inline handler's own parser. Its failures are the message's, not the moment's.
    /// </summary>
    protected static bool TryParse<TInput>(Func<TInput> parse, out TInput? input)
    {
        try
        {
            input = parse();
            return true;
        }
        catch (Exception exception) when (exception is InvalidOperationException or System.Text.Json.JsonException)
        {
            input = default;
            return false;
        }
    }
}
