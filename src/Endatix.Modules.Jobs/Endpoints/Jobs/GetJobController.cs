using Endatix.Api.Infrastructure;
using Endatix.Jobs.Api.Server.Contract.Controllers;
using Endatix.Jobs.Api.Server.Contract.Models;
using Endatix.Modules.Jobs.Domain;
using Endatix.Modules.Jobs.Persistence;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ContractJobStatus = Endatix.Jobs.Api.Server.Contract.Models.JobStatus;
using DomainJobStatus = Endatix.Core.Abstractions.BackgroundJobs.JobStatus;

namespace Endatix.Modules.Jobs.Endpoints.Jobs;

/// <summary>
/// Reports the state of a single background job.
/// </summary>
/// <remarks>
/// Derives from a contract generated out of <c>jobs-api.yaml</c>, so the route, the response type
/// and the documented status codes come from the specification rather than from this file — a
/// change to any of them is a change to the spec, and the compiler enforces that this class still
/// satisfies it.
/// </remarks>
public sealed class GetJobController(IJobsDbContext dbContext) : JobsApiController
{
    /// <inheritdoc />
    public override async Task<IActionResult> JobsGetById(string jobId)
    {
        // The route constraint has already rejected anything that is not a decimal string, so the
        // only parse failure left is a value too large for a long.
        if (!long.TryParse(jobId, out var id))
        {
            return NotFound(JobNotFound());
        }

        // The context's tenant filter scopes this to the caller's tenant, so a job belonging to
        // another tenant is indistinguishable from one that does not exist — which is what stops
        // the endpoint being used to probe for foreign job identifiers.
        var job = await dbContext.BackgroundJobs
            .AsNoTracking()
            .SingleOrDefaultAsync(candidate => candidate.Id == id, HttpContext.RequestAborted);

        return job is null ? NotFound(JobNotFound()) : Ok(ToResponse(job));
    }

    private ProblemDetails JobNotFound() =>
        EndatixProblemDetails.Create(
            StatusCodes.Status404NotFound,
            title: null,
            detail: "No job was found with the supplied identifier.",
            httpContext: HttpContext);

    private static JobResponse ToResponse(BackgroundJob job) => new()
    {
        Id = job.Id.ToString(),
        Type = job.JobType,
        // Mapped by name, never by cast: the generated enum numbers from 1 and the domain enum
        // from 0, so a numeric conversion would silently report the neighbouring status.
        Status = Enum.Parse<ContractJobStatus>(job.Status.ToString()),
        ProgressPercentage = job.ProgressPercentage,
        StatusMessage = job.StatusMessage,
        ErrorMessage = job.ErrorMessage,
        Result = job.Status == DomainJobStatus.Completed ? ToResult(job) : null,
    };

    private static JobResult? ToResult(BackgroundJob job) =>
        job.ResultJson is null ? null : new JobResult();
}
