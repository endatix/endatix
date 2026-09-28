using Endatix.Core.Infrastructure.Result;

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
public abstract class BackgroundJobHandler<TPayload> : IBackgroundJobHandler
    where TPayload : IBackgroundJobPayload
{
    /// <inheritdoc />
    public string JobType => TPayload.JobType;

    /// <inheritdoc />
    public Task<Result> ExecuteAsync(BackgroundJobContext job, CancellationToken cancellationToken)
    {
        if (!BackgroundJobPayloadSerializer.TryDeserialize<TPayload>(job.PayloadJson, out var payload))
        {
            // Names the job type and nothing from the payload, which may hold data the status endpoint must
            // not return.
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
}
