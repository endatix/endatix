using Endatix.Core.Abstractions.BackgroundJobs;
using Endatix.Core.Entities;
using Endatix.Core.Infrastructure.Domain;
using Endatix.Core.Infrastructure.Result;
using Endatix.Core.Specifications;
using Endatix.Infrastructure.Features.Outbox;
using Endatix.Outbox.Engine;
using Microsoft.Extensions.Logging;

namespace Endatix.Modules.Audience.Features.BackgroundJobs;

/// <summary>
/// An Audience outbox handler's work, run as a background job: the job loads its outbox message, reads it with the
/// inline handler's parser and hands it to the same code the relay runs inline.
/// </summary>
/// <remarks>
/// A message that is gone or cannot be read is a failure no retry can fix. The row is read under the job's tenant
/// and the parser checks the payload's tenant against the row's, so a job never works on another tenant's data.
/// The work itself throws on a transient problem, and the job retries.
/// </remarks>
internal abstract class AudienceOutboxJobHandler<TPayload, TInput>(IRepository<OutboxMessage> outboxMessages, ILogger logger)
    : BackgroundJobHandler<TPayload>(logger)
    where TPayload : IAudienceOutboxJobPayload
{
    // Each attempt re-reads the outbox row, so outbox retention must allow for it: see background-processing.mdx.
    public const string MessageGone = "The outbox message no longer exists.";
    public const string MessageUnreadable = "The outbox message could not be read.";

    protected sealed override async Task<Result> ExecuteAsync(
        BackgroundJobContext job,
        TPayload payload,
        CancellationToken cancellationToken)
    {
        // Scoped to the tenant explicitly: outside a request nothing else scopes this read.
        var row = await outboxMessages.FirstOrDefaultAsync(
            new OutboxMessageByIdForTenantSpec(payload.OutboxMessageId, job.TenantId),
            cancellationToken);

        return row is null
            ? Failure(MessageGone)
            : await RunAsync(new OutboxMessageView(row), cancellationToken);
    }

    private async Task<Result> RunAsync(IOutboxMessage message, CancellationToken cancellationToken)
    {
        if (!TryParse(message, out var input))
        {
            return Failure(MessageUnreadable);
        }

        // A message the work has nothing to do for, such as a change that left the test flag alone, is done.
        if (input is not null)
        {
            await ProcessAsync(input, cancellationToken);
        }

        return Result.Success();
    }

    /// <summary>The inline handler's parser. Its failures are the message's, not the moment's.</summary>
    protected abstract TInput? Parse(IOutboxMessage message);

    /// <summary>The inline handler's work.</summary>
    protected abstract Task ProcessAsync(TInput input, CancellationToken cancellationToken);

    private static Result Failure(string message) => Result.Invalid(new ValidationError(message));

    private bool TryParse(IOutboxMessage message, out TInput? input)
    {
        try
        {
            input = Parse(message);
            return true;
        }
        catch (Exception exception) when (exception is InvalidOperationException or System.Text.Json.JsonException)
        {
            input = default;
            return false;
        }
    }
}
