using Endatix.Core.Abstractions.BackgroundJobs;
using Endatix.Core.Infrastructure.Messaging;
using Endatix.Core.Infrastructure.Result;
using Endatix.Modules.Jobs.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Endatix.Modules.Jobs.Features.GetJob;

/// <summary>
/// Reads the current state of one background job for a tenant.
/// </summary>
public sealed record GetJobQuery(long TenantId, long JobId) : IQuery<Result<JobDto>>;

/// <summary>
/// A job's state as reported to a caller.
/// </summary>
/// <remarks>
/// <para>
/// <c>ProgressPercentage</c> and <c>StatusMessage</c> are advisory: a handler that reports nothing
/// stays at zero while running perfectly well, so neither is a liveness signal and neither should be
/// used to decide a job has stalled. <c>Status</c> is the authoritative value.
/// </para>
/// <para>
/// Whatever a job produced is deliberately absent. The job row records it in <c>ResultJson</c>, but
/// that column is written by whichever handler ran the job, so the only shape this contract could
/// promise across every job type is a free-form object — which tells a client nothing it can rely
/// on. A job type whose output a caller genuinely needs should expose it through the endpoint that
/// owns that output, where it can be typed.
/// </para>
/// </remarks>
public sealed record JobDto(
    long Id,
    string Type,
    JobStatus Status,
    int ProgressPercentage,
    string? StatusMessage,
    string? ErrorMessage);

internal sealed class GetJobHandler(IJobsDbContext dbContext)
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
            .Select(candidate => new JobDto(
                candidate.Id,
                candidate.JobType,
                candidate.Status,
                candidate.ProgressPercentage,
                candidate.StatusMessage,
                candidate.ErrorMessage))
            .FirstOrDefaultAsync(cancellationToken);

        return job is null
            ? Result.NotFound($"Job with ID {request.JobId} was not found.")
            : Result.Success(job);
    }
}
