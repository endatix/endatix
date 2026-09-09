using System.Text.Json;
using Endatix.Core.Abstractions.BackgroundJobs;
using Endatix.Core.Infrastructure.Messaging;
using Endatix.Core.Infrastructure.Result;
using Endatix.Modules.Jobs.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Endatix.Modules.Jobs.Features.GetJob;

/// <summary>
/// Reads the current state of one background job for a tenant.
/// </summary>
public sealed record GetJobQuery(long TenantId, long JobId) : IQuery<Result<JobDto>>;

/// <summary>
/// A job's state as reported to a caller.
/// </summary>
/// <remarks>
/// <c>ProgressPercentage</c> and <c>StatusMessage</c> are advisory: a handler that reports nothing
/// stays at zero while running perfectly well, so neither is a liveness signal and neither should be
/// used to decide a job has stalled. <c>Status</c> is the authoritative value.
/// </remarks>
public sealed record JobDto(
    long Id,
    string Type,
    JobStatus Status,
    int ProgressPercentage,
    string? StatusMessage,
    JsonElement? Result,
    string? ErrorMessage);

internal sealed class GetJobHandler(IJobsDbContext dbContext, ILogger<GetJobHandler> logger)
    : IQueryHandler<GetJobQuery, Result<JobDto>>
{
    public async Task<Result<JobDto>> Handle(GetJobQuery request, CancellationToken cancellationToken)
    {
        // The ambient tenant filter cannot carry this on its own. It reads a tenant id of zero as
        // "no tenant, show everything" so that background services can sweep across tenants — and a
        // request whose principal has no usable `tid` claim also leaves the tenant context at zero,
        // because TenantMiddleware returns early rather than failing. Scoping such a request by the
        // filter alone would serve it every tenant's jobs.
        if (request.TenantId <= 0)
        {
            return Result.Unauthorized("Tenant context is required.");
        }

        var job = await dbContext.BackgroundJobs
            .Where(candidate => candidate.Id == request.JobId && candidate.TenantId == request.TenantId)
            .Select(candidate => new
            {
                candidate.Id,
                candidate.JobType,
                candidate.Status,
                candidate.ProgressPercentage,
                candidate.StatusMessage,
                candidate.ResultJson,
                candidate.ErrorMessage,
            })
            .FirstOrDefaultAsync(cancellationToken);

        if (job is null)
        {
            return Result.NotFound($"Job with ID {request.JobId} was not found.");
        }

        return Result.Success(new JobDto(
            job.Id,
            job.JobType,
            job.Status,
            job.ProgressPercentage,
            job.StatusMessage,
            // Only Complete writes ResultJson, and it is terminal, so no other status can carry one.
            // The gate is defence against a future writer rather than a state reachable today.
            job.Status == JobStatus.Completed ? ParseResult(job.Id, job.ResultJson) : null,
            job.ErrorMessage));
    }

    /// <remarks>
    /// Passed through as written rather than mapped onto a fixed shape. Job types produce different
    /// things — an export produces a file, a webhook delivery produces a response code — so the only
    /// shape this endpoint could describe for every type is the one the handler chose. Each job type
    /// documents its own; a caller already knows which type it asked about.
    /// </remarks>
    private JsonElement? ParseResult(long jobId, string? resultJson)
    {
        if (string.IsNullOrWhiteSpace(resultJson))
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<JsonElement>(resultJson);
        }
        catch (JsonException exception)
        {
            // Reporting no result keeps the status readable, but silence would leave a completed job
            // claiming to have produced nothing, on every poll, with nowhere to look for the reason.
            logger.LogWarning(
                exception,
                "Background job {JobId} completed with a result payload that is not valid JSON; reporting no result.",
                jobId);
            return null;
        }
    }
}
