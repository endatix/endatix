using Endatix.Core.Abstractions.BackgroundJobs;
using Endatix.Core.Entities;
using Endatix.Core.Infrastructure.Domain;
using Endatix.Core.Infrastructure.Result;
using Endatix.Core.Specifications;
using Endatix.Infrastructure.Features.BackgroundJobs;
using Endatix.Infrastructure.Features.Outbox;
using Endatix.Modules.Audience.Features.Forms;
using Endatix.Modules.Audience.Features.TestSubmissions;
using Endatix.Outbox.Engine;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Endatix.Modules.Audience.Features.BackgroundJobs;

/// <summary>
/// Registers Audience's outbox work: the inline handlers the relay runs, and the same work as background jobs with
/// the subscriptions that enqueue them. Which one delivers is the relay's switch
/// (<c>Endatix:Outbox:DeliverToJobQueue</c>); without the subscriptions the host refuses to start with it on.
/// </summary>
internal static class AudienceOutboxJobs
{
    // Each job is one short update, as in Reporting: five attempts doubling from 10 seconds.
    private static readonly BackgroundJobTypeDefaults JobDefaults = new()
    {
        MaxAttempts = 5,
        MaxRuntimeMinutes = 10,
        BackoffBaseSeconds = 10,
        BackoffCapSeconds = 600,
        RetentionDays = 3,
        MaxConcurrency = 2,
    };

    public static IServiceCollection AddAudienceOutboxWork(this IServiceCollection services)
    {
        AddInline<DeleteFormAudienceOutboxHandler>(services);
        AddInline<ReleaseLinkOnTestMarkOutboxHandler>(services);

        services.AddBackgroundJobHandler<DeleteFormAudienceJobHandler, AudienceDeleteFormAudiencePayload>(JobDefaults);
        services.AddBackgroundJobHandler<ReleaseLinkOnTestMarkJobHandler, AudienceReleaseLinkOnTestMarkPayload>(JobDefaults);

        Subscribe(services, DeleteFormAudienceOutboxHandler.HandledEventTypes,
            message => new AudienceDeleteFormAudiencePayload(message.Id));
        foreach (var eventType in ReleaseLinkOnTestMarkOutboxHandler.HandledEventTypes)
        {
            services.AddOutboxJobSubscription<AudienceReleaseLinkOnTestMarkPayload, ReleaseLinkOnTestMarkExpander>(eventType);
        }

        return services;
    }

    // Each inline handler is registered as itself too, so its job handler reuses the very same work.
    private static void AddInline<THandler>(IServiceCollection services)
        where THandler : class, IOutboxIntegrationEventHandler
    {
        services.AddScoped<THandler>();
        services.AddScoped<IOutboxIntegrationEventHandler>(provider => provider.GetRequiredService<THandler>());
    }

    private static void Subscribe<TPayload>(
        IServiceCollection services,
        IEnumerable<string> eventTypes,
        Func<IOutboxMessage, TPayload> createPayload)
        where TPayload : IBackgroundJobPayload
    {
        foreach (var eventType in eventTypes)
        {
            services.AddOutboxJobSubscription(eventType, createPayload);
        }
    }
}

/// <summary>
/// Enqueues the link release only for a test mark: every other <c>submission.updated</c> (answers, metadata, a new
/// submitter) has nothing to free, and would only add a job that does nothing.
/// </summary>
/// <remarks>
/// A payload that cannot be read is enqueued anyway, so its job fails visibly (<c>MessageUnreadable</c>) instead of
/// the relay dropping it.
/// </remarks>
internal sealed class ReleaseLinkOnTestMarkExpander : IOutboxSubscriberExpander<AudienceReleaseLinkOnTestMarkPayload>
{
    public Task<IReadOnlyList<OutboxSubscriber<AudienceReleaseLinkOnTestMarkPayload>>> ExpandAsync(
        IOutboxMessage message,
        long tenantId,
        CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<OutboxSubscriber<AudienceReleaseLinkOnTestMarkPayload>>>(
            MayBeATestMark(message)
                ?
                [
                    new OutboxSubscriber<AudienceReleaseLinkOnTestMarkPayload>(
                        AudienceReleaseLinkOnTestMarkPayload.JobType,
                        new AudienceReleaseLinkOnTestMarkPayload(message.Id)),
                ]
                : []);

    private static bool MayBeATestMark(IOutboxMessage message)
    {
        try
        {
            return ReleaseLinkOnTestMarkOutboxHandler.Parse(message) is not null;
        }
        catch (Exception exception) when (exception is InvalidOperationException or System.Text.Json.JsonException)
        {
            return true;
        }
    }
}

/// <summary>The input of every Audience job: the outbox message whose event it processes.</summary>
internal interface IAudienceOutboxJobPayload : IBackgroundJobPayload
{
    long OutboxMessageId { get; }
}

internal sealed record AudienceDeleteFormAudiencePayload(long OutboxMessageId) : IAudienceOutboxJobPayload
{
    public static string JobType => "AudienceDeleteFormAudience";
}

internal sealed record AudienceReleaseLinkOnTestMarkPayload(long OutboxMessageId) : IAudienceOutboxJobPayload
{
    public static string JobType => "AudienceReleaseLinkOnTestMark";
}

/// <summary>
/// An Audience outbox handler's work, run as a background job: the job loads its outbox message, parses it with the
/// inline handler's own parser and runs the same work.
/// </summary>
/// <remarks>
/// A message that is gone or cannot be read, or that names another tenant, is a failure no retry can fix. The work
/// itself throws on a transient problem, and the job retries.
/// </remarks>
internal abstract class AudienceOutboxJobHandler<TPayload, TInput>(IRepository<OutboxMessage> outboxMessages, ILogger logger)
    : BackgroundJobHandler<TPayload>(logger)
    where TPayload : IAudienceOutboxJobPayload
{
    public const string MessageGone = "The outbox message no longer exists.";
    public const string MessageUnreadable = "The outbox message could not be read.";
    public const string OtherTenant = "The outbox message belongs to another tenant.";

    protected sealed override async Task<Result> ExecuteAsync(
        BackgroundJobContext job,
        TPayload payload,
        CancellationToken cancellationToken)
    {
        // Scoped to the tenant explicitly: outside a request nothing else scopes this read.
        var row = await outboxMessages.FirstOrDefaultAsync(
            new OutboxMessageByIdForTenantSpec(payload.OutboxMessageId, job.TenantId),
            cancellationToken);
        if (row is null)
        {
            return Failure(MessageGone);
        }

        TInput input;
        try
        {
            input = Parse(new OutboxMessageView(row));
        }
        catch (Exception exception) when (exception is InvalidOperationException or System.Text.Json.JsonException)
        {
            return Failure(MessageUnreadable);
        }

        if (TenantOf(input) is long tenantId && tenantId != job.TenantId)
        {
            return Failure(OtherTenant);
        }

        await ProcessAsync(input, cancellationToken);
        return Result.Success();
    }

    protected abstract TInput Parse(IOutboxMessage message);

    /// <summary>The tenant the message names; <see langword="null"/> when it names nothing to do.</summary>
    protected abstract long? TenantOf(TInput input);

    protected abstract Task ProcessAsync(TInput input, CancellationToken cancellationToken);

    private static Result Failure(string message) => Result.Invalid(new ValidationError(message));
}

/// <summary>Soft-deletes a deleted form's audience.</summary>
internal sealed class DeleteFormAudienceJobHandler(
    IRepository<OutboxMessage> outboxMessages,
    DeleteFormAudienceOutboxHandler work,
    ILogger<DeleteFormAudienceJobHandler> logger)
    : AudienceOutboxJobHandler<AudienceDeleteFormAudiencePayload, DeleteFormAudienceOutboxHandler.DeletedForm>(outboxMessages, logger)
{
    protected override DeleteFormAudienceOutboxHandler.DeletedForm Parse(IOutboxMessage message) =>
        DeleteFormAudienceOutboxHandler.Parse(message);

    protected override long? TenantOf(DeleteFormAudienceOutboxHandler.DeletedForm input) => input.TenantId;

    protected override Task ProcessAsync(DeleteFormAudienceOutboxHandler.DeletedForm input, CancellationToken cancellationToken) =>
        work.ProcessAsync(input, cancellationToken);
}

/// <summary>Frees the link of a submission marked as test.</summary>
internal sealed class ReleaseLinkOnTestMarkJobHandler(
    IRepository<OutboxMessage> outboxMessages,
    ReleaseLinkOnTestMarkOutboxHandler work,
    ILogger<ReleaseLinkOnTestMarkJobHandler> logger)
    : AudienceOutboxJobHandler<AudienceReleaseLinkOnTestMarkPayload, ReleaseLinkOnTestMarkOutboxHandler.MarkedSubmission?>(outboxMessages, logger)
{
    protected override ReleaseLinkOnTestMarkOutboxHandler.MarkedSubmission? Parse(IOutboxMessage message) =>
        ReleaseLinkOnTestMarkOutboxHandler.Parse(message);

    // Any other submission update has nothing to free.
    protected override long? TenantOf(ReleaseLinkOnTestMarkOutboxHandler.MarkedSubmission? input) => input?.TenantId;

    protected override Task ProcessAsync(
        ReleaseLinkOnTestMarkOutboxHandler.MarkedSubmission? input,
        CancellationToken cancellationToken) =>
        work.ProcessAsync(input, cancellationToken);
}
