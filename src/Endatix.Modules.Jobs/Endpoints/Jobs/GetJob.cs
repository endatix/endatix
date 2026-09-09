using Endatix.Api.Infrastructure;
using Endatix.Core.Abstractions;
using Endatix.Core.Abstractions.Authorization;
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
            summary.Responses[404] = "Job not found.";
        });
        Description(builder => builder
            .Produces<JobResponse>(200, "application/json")
            .ProducesProblem(400)
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
