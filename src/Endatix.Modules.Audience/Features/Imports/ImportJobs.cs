using Endatix.Core.Abstractions.BackgroundJobs;
using Endatix.Core.Infrastructure.Result;
using Endatix.Infrastructure.Features.BackgroundJobs;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Endatix.Modules.Audience.Features.Imports;

/// <summary>Runs the Check of a staged import.</summary>
internal sealed record CheckImportPayload(long FormId, long ImportId) : IBackgroundJobPayload
{
    // Persisted on the job row and in the scheduler's keys: never change it.
    public static string JobType => "AudienceImportCheck";
}

/// <summary>
/// Runs, or continues, the write of a confirmed import for one write run; the run is null on jobs enqueued before
/// runs were named.
/// </summary>
internal sealed record WriteImportPayload(long FormId, long ImportId, int? WriteRun = null) : IBackgroundJobPayload
{
    // Persisted on the job row and in the scheduler's keys: never change it.
    public static string JobType => "AudienceImportWrite";
}

internal sealed class CheckImportJobHandler(ImportRun run, ILogger<CheckImportJobHandler> logger)
    : BackgroundJobHandler<CheckImportPayload>(logger)
{
    protected override Task<Result> ExecuteAsync(
        BackgroundJobContext job,
        CheckImportPayload payload,
        CancellationToken cancellationToken) =>
        run.RunAsJobAsync(new ImportRef(job.TenantId, payload.FormId, payload.ImportId), ImportPhase.Check, cancellationToken);
}

internal sealed class WriteImportJobHandler(ImportRun run, ILogger<WriteImportJobHandler> logger)
    : BackgroundJobHandler<WriteImportPayload>(logger)
{
    protected override Task<Result> ExecuteAsync(
        BackgroundJobContext job,
        WriteImportPayload payload,
        CancellationToken cancellationToken) =>
        run.RunAsJobAsync(
            new ImportRef(job.TenantId, payload.FormId, payload.ImportId, payload.WriteRun), ImportPhase.Write, cancellationToken);
}

internal static class ImportJobs
{
    public static IServiceCollection AddImportJobs(this IServiceCollection services) =>
        services
            .AddBackgroundJobHandler<CheckImportJobHandler, CheckImportPayload>(
                new BackgroundJobTypeDefaults { MaxAttempts = 3, MaxConcurrency = 2 })
            // A write continues from its checkpoint, so retries are cheap and come soon.
            .AddBackgroundJobHandler<WriteImportJobHandler, WriteImportPayload>(
                new BackgroundJobTypeDefaults
                {
                    MaxAttempts = 8,
                    MaxConcurrency = 2,
                    BackoffBaseSeconds = 5,
                    BackoffCapSeconds = 300,
                });
}
