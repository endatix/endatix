using Endatix.Core.Abstractions.BackgroundJobs;
using Endatix.Core.Infrastructure.Result;
using Endatix.Modules.Audience.Domain;
using Endatix.Modules.Audience.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Endatix.Modules.Audience.Features.Imports;

/// <summary>
/// Starts a phase of a staged import once the transition into it has committed: as a background job where the host
/// has the job queue, inline in the request where it has none. Callers never branch on which.
/// </summary>
internal interface IImportRunner
{
    bool RunsInBackground { get; }

    /// <summary>
    /// Starts the phase. Safe to call again for the same phase and run: a queued phase is enqueued once.
    /// </summary>
    Task<Result> StartAsync(ImportRef import, ImportPhase phase, CancellationToken cancellationToken);
}

/// <summary>Enqueues each phase as a background job and records the job on the import.</summary>
internal sealed class QueuedImportRunner(IBackgroundJobQueue queue, IAudienceDbContext db) : IImportRunner
{
    public bool RunsInBackground => true;

    public async Task<Result> StartAsync(ImportRef import, ImportPhase phase, CancellationToken cancellationToken)
    {
        AudienceImport? row = await db.AudienceImports.SingleOrDefaultAsync(
            item => item.Id == import.ImportId && item.TenantId == import.TenantId,
            cancellationToken);
        if (row is null)
        {
            return Result.NotFound("Import not found.");
        }

        long jobId = await queue.EnqueueAsync(Request(import, phase, row), cancellationToken);
        await RecordJobAsync(row, jobId, cancellationToken);
        return Result.Success();
    }

    /// <summary>
    /// Links the import to its job for operators. When the job moved the import first, the job is enqueued all the
    /// same; only that link is lost.
    /// </summary>
    private async Task RecordJobAsync(AudienceImport row, long jobId, CancellationToken cancellationToken)
    {
        row.RecordJob(jobId);
        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            ((DbContext)db).Entry(row).State = EntityState.Unchanged;
        }
    }

    // The key names the phase of one run, so enqueueing it again returns the same job. A resumed write is a new run
    // with a key of its own.
    private static BackgroundJobRequest Request(ImportRef import, ImportPhase phase, AudienceImport row) =>
        phase == ImportPhase.Check
            ? BackgroundJobRequest.Create(
                new CheckImportPayload(import.FormId, import.ImportId),
                import.TenantId,
                row.PerformedById,
                dedupKey: $"{import.ImportId}:check")
            : BackgroundJobRequest.Create(
                new WriteImportPayload(import.FormId, import.ImportId),
                import.TenantId,
                row.PerformedById,
                dedupKey: $"{import.ImportId}:write:{row.WriteRun}");
}

/// <summary>Runs each phase in the request, for hosts without the job queue; the file limit keeps that short.</summary>
internal sealed class InlineImportRunner(ImportRun run) : IImportRunner
{
    public bool RunsInBackground => false;

    /// <summary>
    /// A phase the request leaves running (a closed page, a proxy timeout, an error, a busy audience) has no job to
    /// continue it, so it stops as Failed, on a token the aborted request does not cancel. A stopped write resumes.
    /// </summary>
    public async Task<Result> StartAsync(ImportRef import, ImportPhase phase, CancellationToken cancellationToken)
    {
        try
        {
            return await run.RunAsync(import, phase, cancellationToken);
        }
        finally
        {
            await run.StopIfRunningAsync(import, CancellationToken.None);
        }
    }
}

/// <summary>How large a file this host imports: larger in the background than in one request.</summary>
public sealed record ImportLimitsDto(int MaxRows, long MaxBytes, bool RunsInBackground);

internal static class ImportRunnerRegistration
{
    public const int ChunkRows = 1_000;

    public static ImportLimitsDto LimitsOf(IImportRunner runner) =>
        runner.RunsInBackground
            ? new ImportLimitsDto(100_000, 50L * 1024 * 1024, RunsInBackground: true)
            : new ImportLimitsDto(Shared.ImportLimits.MaxRows, 20L * 1024 * 1024, RunsInBackground: false);

    /// <summary>The queue runner when the Jobs module registered a queue, else the inline one.</summary>
    public static IServiceCollection AddImportRunner(this IServiceCollection services)
    {
        services.AddSingleton<ImportRun>();
        services.AddScoped<IImportRunner>(provider =>
            provider.GetService<IBackgroundJobQueue>() is { } queue
                ? new QueuedImportRunner(queue, provider.GetRequiredService<IAudienceDbContext>())
                : new InlineImportRunner(provider.GetRequiredService<ImportRun>()));
        return services;
    }
}
