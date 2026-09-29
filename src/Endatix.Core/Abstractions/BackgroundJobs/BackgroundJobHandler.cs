using System.Text.Json;
using Endatix.Core.Infrastructure.Result;
using Microsoft.Extensions.Logging;

namespace Endatix.Core.Abstractions.BackgroundJobs;

/// <summary>
/// Base class for a handler whose input is a typed <typeparamref name="TPayload"/>. It reads the job type
/// from the payload type and deserializes the stored input, so a derived handler receives typed data and
/// never parses JSON itself.
/// </summary>
/// <remarks>
/// Input that cannot be read returns a failure <see cref="Result"/> instead of throwing: no retry can repair
/// stored data, so the job ends <c>Failed</c> on its first attempt. The contract on
/// <see cref="IBackgroundJobHandler"/> applies to the derived handler unchanged.
/// </remarks>
/// <param name="logger">
/// Records why input could not be read, which the failure <see cref="Result"/> deliberately omits.
/// </param>
public abstract partial class BackgroundJobHandler<TPayload>(ILogger logger) : IBackgroundJobHandler
    where TPayload : IBackgroundJobPayload
{
    /// <inheritdoc />
    public string JobType => TPayload.JobType;

    /// <inheritdoc />
    public Task<Result> ExecuteAsync(BackgroundJobContext job, CancellationToken cancellationToken)
    {
        TPayload payload;
        try
        {
            payload = BackgroundJobPayloadSerializer.Deserialize<TPayload>(job.PayloadJson);
        }
        catch (JsonException ex)
        {
            // The log carries the reader's error, which names the offending member and position. Neither the
            // log nor the result carries the payload itself, which may hold data the status endpoint must not
            // return.
            LogUnreadablePayload(logger, job.JobId, job.JobType, job.TenantId, job.AttemptCount, ex);

            return Task.FromResult(Result.Invalid(
                new ValidationError($"The job's input could not be read ({TPayload.JobType}).")));
        }

        return ExecuteAsync(job, payload, cancellationToken);
    }

    /// <summary>
    /// Executes the job with its typed input. See <see cref="IBackgroundJobHandler"/> for when to return a
    /// failure and when to throw.
    /// </summary>
    protected abstract Task<Result> ExecuteAsync(
        BackgroundJobContext job,
        TPayload payload,
        CancellationToken cancellationToken);

    [LoggerMessage(
        Level = LogLevel.Warning,
        EventName = "BackgroundJobPayloadUnreadable",
        Message = "Background job {JobId} ({JobType}) for tenant {TenantId} failed on attempt {AttemptCount}: its input could not be read")]
    private static partial void LogUnreadablePayload(
        ILogger logger,
        long jobId,
        string jobType,
        long tenantId,
        int attemptCount,
        Exception exception);
}
