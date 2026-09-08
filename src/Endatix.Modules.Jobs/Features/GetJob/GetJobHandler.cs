using System.Text.Json;
using Endatix.Core.Abstractions.BackgroundJobs;
using Endatix.Core.Infrastructure.Messaging;
using Endatix.Core.Infrastructure.Result;
using Endatix.Modules.Jobs.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Endatix.Modules.Jobs.Features.GetJob;

/// <summary>
/// Reads the current state of one background job.
/// </summary>
public sealed record GetJobQuery(long JobId) : IQuery<Result<JobDto>>;

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
    JobResultDto? Result,
    string? ErrorMessage);

/// <summary>
/// Where to collect what a completed job produced.
/// </summary>
public sealed record JobResultDto(string? DownloadUrl, string? FileName, string? ContentType);

/// <remarks>
/// No tenant argument: the jobs context filters every query to the current tenant, so a job
/// belonging to another one is simply not found. Passing a tenant id here as well would add a
/// second, weaker guard that could disagree with the first.
/// </remarks>
internal sealed class GetJobHandler(IJobsDbContext dbContext)
    : IQueryHandler<GetJobQuery, Result<JobDto>>
{
    private static readonly JsonSerializerOptions ResultJsonOptions =
        new(JsonSerializerDefaults.Web);

    public async Task<Result<JobDto>> Handle(GetJobQuery request, CancellationToken cancellationToken)
    {
        var job = await dbContext.BackgroundJobs
            .AsNoTracking()
            .Where(candidate => candidate.Id == request.JobId)
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
            .SingleOrDefaultAsync(cancellationToken);

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
            // Only a completed job has produced anything. A failed one may still carry the result of
            // an earlier successful attempt, and offering that as this job's output would hand the
            // caller a stale artifact under a job that did not finish.
            job.Status == JobStatus.Completed ? ParseResult(job.ResultJson) : null,
            job.ErrorMessage));
    }

    /// <remarks>
    /// The column is written by whichever handler ran the job, so its contents are not guaranteed to
    /// match this shape. A payload that does not parse is reported as no result rather than failing
    /// the read — the caller still needs the status.
    /// </remarks>
    private static JobResultDto? ParseResult(string? resultJson)
    {
        if (string.IsNullOrWhiteSpace(resultJson))
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<JobResultDto>(resultJson, ResultJsonOptions);
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
