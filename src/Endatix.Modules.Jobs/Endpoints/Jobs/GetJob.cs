using Endatix.Api.Infrastructure;
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
/// Open to any authenticated member of the tenant that owns the job, with no further permission.
/// A job carries no data of its own beyond its progress — the work it did is reached through
/// whatever endpoint owns that work, which applies its own permissions there.
/// </remarks>
public sealed class GetJob(IMediator mediator)
    : Endpoint<GetJobRequest, Results<Ok<JobResponse>, ProblemHttpResult>>
{
    /// <inheritdoc />
    public override void Configure()
    {
        Get("jobs/{jobId}");
        Summary(summary =>
        {
            summary.Summary = "Get a background job";
            summary.Description =
                "Retrieves the current state of a background job. Progress and status message are " +
                "advisory and may lag the work itself; status is authoritative. Download metadata " +
                "appears only once the job has completed successfully.";
            summary.ExampleRequest = new GetJobRequest { JobId = 987654321 };
            summary.Responses[200] = "Job state retrieved.";
            summary.Responses[404] = "Job not found.";
        });
        Description(builder => builder
            .Produces<JobResponse>(200, "application/json")
            .ProducesProblem(404));
    }

    /// <inheritdoc />
    public override async Task<Results<Ok<JobResponse>, ProblemHttpResult>> ExecuteAsync(
        GetJobRequest request,
        CancellationToken ct)
    {
        var result = await mediator.Send(new GetJobQuery(request.JobId), ct);

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
