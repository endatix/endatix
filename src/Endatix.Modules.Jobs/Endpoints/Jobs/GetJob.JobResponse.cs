using System.Text.Json.Nodes;
using Endatix.Core.Abstractions.BackgroundJobs;
using Endatix.Modules.Jobs.Features.GetJob;

namespace Endatix.Modules.Jobs.Endpoints.Jobs;

/// <summary>
/// The state of a background job.
/// </summary>
public sealed class JobResponse
{
    /// <summary>
    /// The job identifier. Serialized as a string, like every other Endatix identifier, because the
    /// values exceed what a JSON number represents exactly.
    /// </summary>
    public long Id { get; init; }

    /// <summary>
    /// The job type, which decides the handler that runs it — for example <c>SubmissionExport</c>.
    /// </summary>
    public string Type { get; init; } = string.Empty;

    /// <summary>
    /// The authoritative lifecycle state.
    /// </summary>
    public JobStatus Status { get; init; }

    /// <summary>
    /// Completion estimate for display. Advisory: a handler that reports nothing stays at zero while
    /// running normally, so this is never a liveness signal.
    /// </summary>
    public int ProgressPercentage { get; init; }

    /// <summary>
    /// Human-readable phase, when the handler reports one.
    /// </summary>
    public string? StatusMessage { get; init; }

    /// <summary>
    /// What the job produced, in the shape its handler wrote. Null until the job completes, and for
    /// job types that produce nothing.
    /// </summary>
    /// <remarks>
    /// Deliberately untyped. One queue serves every job type, and they produce different things — an
    /// export produces a file, a webhook delivery produces a response code — so there is no single
    /// shape this endpoint could promise. Each job type documents its own, and a caller already knows
    /// which type it asked about from <see cref="Type"/>.
    /// </remarks>
    public JsonNode? Result { get; init; }

    /// <summary>
    /// Why the job failed. Set only once it has failed or been dead-lettered.
    /// </summary>
    public string? ErrorMessage { get; init; }

    internal static JobResponse FromDto(JobDto job) => new()
    {
        Id = job.Id,
        Type = job.Type,
        Status = job.Status,
        ProgressPercentage = job.ProgressPercentage,
        StatusMessage = job.StatusMessage,
        Result = job.Result,
        ErrorMessage = job.ErrorMessage,
    };
}
