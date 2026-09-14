using Endatix.Api.Infrastructure;
using Endatix.Core.Abstractions;
using Endatix.Core.Abstractions.Authorization;
using Endatix.Core.Abstractions.BackgroundJobs;
using Endatix.Modules.Jobs.Features.GetJob;
using FastEndpoints;
using FluentValidation;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Endatix.Modules.Jobs.Endpoints.Jobs;

/// <summary>
/// Reports the state of a single background job.
/// </summary>
/// <remarks>
/// Requires <see cref="Actions.Jobs.View"/> and membership of the tenant that owns the job. The
/// permission is deliberately coarse: reporting on a job says only how far the work got, and the
/// work itself is reached through whatever endpoint owns it, which applies its own permission
/// there. A per-job-type check belongs with the job types that need one.
/// </remarks>
public sealed class GetJob(IMediator mediator, ITenantContext tenantContext)
    : Endpoint<GetJobRequest, Results<Ok<JobResponse>, ProblemHttpResult>>
{
    /// <inheritdoc />
    public override void Configure()
    {
        Get("jobs/{jobId}");
        Permissions(Actions.Jobs.View);
        Summary(summary =>
        {
            summary.Summary = "Get a background job";
            summary.Description =
                "Retrieves the current state of a background job. Progress and status message are " +
                "advisory and may lag the work itself; status is authoritative.";
            summary.ExampleRequest = new GetJobRequest { JobId = 987654321 };
            summary.Responses[200] = "Job state retrieved.";
            summary.Responses[400] = "The job ID is not valid.";
            summary.Responses[401] = "The request carries no tenant.";
            summary.Responses[404] = "Job not found.";
        });
        Description(builder => builder
            .Produces<JobResponse>(200, "application/json")
            .ProducesProblem(400)
            .ProducesProblem(401)
            .ProducesProblem(404));
    }

    /// <inheritdoc />
    public override async Task<Results<Ok<JobResponse>, ProblemHttpResult>> ExecuteAsync(
        GetJobRequest request,
        CancellationToken ct)
    {
        var result = await mediator.Send(new GetJobQuery(tenantContext.TenantId, request.JobId), ct);

        return TypedResultsBuilder
            .MapResult(result, JobResponse.FromDto)
            .SetTypedResults<Ok<JobResponse>, ProblemHttpResult>();
    }
}

/// <summary>
/// Validates the request for the get job endpoint.
/// </summary>
public sealed class GetJobValidator : Validator<GetJobRequest>
{
    public GetJobValidator()
    {
        RuleFor(request => request.JobId).GreaterThan(0);
    }
}

/// <summary>
/// The request for the get job endpoint.
/// </summary>
public sealed class GetJobRequest
{
    /// <summary>
    /// The ID of the job.
    /// </summary>
    public long JobId { get; init; }
}

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
    /// The most recent failure recorded against the job. Present on <c>Failed</c> and
    /// <c>DeadLettered</c>, and also on <c>Retrying</c> — a job between attempts carries the reason
    /// the last one failed, so this being set does not mean the job has stopped. Read
    /// <see cref="Status"/> for that.
    /// </summary>
    public string? ErrorMessage { get; init; }

    internal static JobResponse FromDto(JobDto job) => new()
    {
        Id = job.Id,
        Type = job.Type,
        Status = job.Status,
        ProgressPercentage = job.ProgressPercentage,
        StatusMessage = job.StatusMessage,
        ErrorMessage = job.ErrorMessage,
    };
}
