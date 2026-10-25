using Endatix.Core.Abstractions;
using Endatix.Modules.Audience.Domain;
using Endatix.Modules.Audience.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Endatix.Modules.Audience.Features.Imports;

/// <summary>
/// Ends imports nobody is moving and deletes what finished imports staged. An upload or a checked import idle a day
/// expires, a Check stalled six hours fails, a write stalled six hours fails resumable (the state a dead-lettered or
/// hung write job leaves), and a stopped write nobody resumed for a week expires. Finished imports lose their chunks,
/// and their rejections a week after they finish. A tenant's import start ends its idle imports, so an abandoned one
/// never holds a form; the daily run does both, for every tenant.
/// </summary>
/// <remarks>
/// Each transition is the aggregate's own, saved against the status it was read with, so sweeps on several nodes,
/// and a run moving the import meanwhile, settle as compare-and-set. Every query names the tenant when there is one:
/// the daily sweep runs without a request, where the ambient tenant filter lets every tenant's rows through.
/// </remarks>
internal sealed class ImportSweep(IAudienceDbContext db, IDateTimeProvider clock)
{
    public static readonly TimeSpan IdleLimit = TimeSpan.FromHours(24);
    public static readonly TimeSpan StallLimit = TimeSpan.FromHours(6);

    /// <summary>How long rejections outlive their import, and a stopped write stays resumable.</summary>
    public static readonly TimeSpan Kept = TimeSpan.FromDays(7);

    internal const string CheckStalled = "The check stopped before it finished. Start the import again.";
    internal const string WriteStalled = "The write stopped before it finished. Resume it to continue where it stopped.";

    private const int Batch = 100;

    /// <summary>What ends an import idle in each status, and after how long.</summary>
    private static readonly IdleEnd[] IdleEnds =
    [
        new(AudienceImportStatusCodes.Uploading, IdleLimit, import => import.Expire()),
        new(AudienceImportStatusCodes.Checked, IdleLimit, import => import.Expire()),
        new(AudienceImportStatusCodes.Checking, StallLimit, import => import.Fail(CheckStalled)),
        new(AudienceImportStatusCodes.Writing, StallLimit, import => import.Fail(WriteStalled, resumable: true)),
        new(AudienceImportStatusCodes.Failed, Kept, import => import.Expire()),
    ];

    /// <summary>Ends the tenant's idle imports; returns how many this call ended.</summary>
    public Task<int> ForTenantAsync(long tenantId, CancellationToken cancellationToken) =>
        EndIdleAsync(tenantId, cancellationToken);

    /// <summary>Ends every tenant's idle imports and deletes finished staging; returns how many imports it ended.</summary>
    public async Task<int> AllTenantsAsync(CancellationToken cancellationToken)
    {
        int ended = await EndIdleAsync(null, cancellationToken);
        await DeleteFinishedStagingAsync(cancellationToken);
        return ended;
    }

    private async Task<int> EndIdleAsync(long? tenantId, CancellationToken cancellationToken)
    {
        DateTime now = clock.Now.UtcDateTime;
        int ended = 0;
        foreach (IdleEnd rule in IdleEnds)
        {
            ended += await EndAsync(new IdleRule(tenantId, rule.Status, now - rule.After), rule.End, cancellationToken);
        }

        return ended;
    }

    /// <summary>Ends, in batches, every import in the rule's status with no change since its cutoff.</summary>
    private async Task<int> EndAsync(IdleRule rule, Action<AudienceImport> end, CancellationToken cancellationToken)
    {
        int ended = 0;
        List<AudienceImport> idle;
        do
        {
            idle = await Idle(rule).Take(Batch).ToListAsync(cancellationToken);
            foreach (AudienceImport import in idle)
            {
                end(import);
                ended += await SaveUnlessMovedAsync(import, cancellationToken) ? 1 : 0;
            }

            ((DbContext)db).ChangeTracker.Clear();
        }
        while (idle.Count == Batch);

        return ended;
    }

    /// <summary>
    /// An upload's chunks count as activity, since sending one changes no import column. A failed import counts only
    /// when it may resume.
    /// </summary>
    private IQueryable<AudienceImport> Idle(IdleRule rule) =>
        Imports(rule.TenantId).Where(import => import.Status == rule.Status
            && (import.ModifiedAt ?? import.CreatedAt) < rule.Cutoff
            && (rule.Status != AudienceImportStatusCodes.Failed || import.Resumable)
            && (rule.Status != AudienceImportStatusCodes.Uploading
                || !db.ImportChunks.Any(chunk => chunk.TenantId == import.TenantId
                    && chunk.ImportId == import.Id
                    && chunk.CreatedAt >= rule.Cutoff)));

    /// <summary>Saves the transition; false when a run or another sweep moved the import first.</summary>
    private async Task<bool> SaveUnlessMovedAsync(AudienceImport import, CancellationToken cancellationToken)
    {
        try
        {
            await db.SaveChangesAsync(cancellationToken);
            return true;
        }
        catch (DbUpdateConcurrencyException)
        {
            ((DbContext)db).Entry(import).State = EntityState.Detached;
            return false;
        }
    }

    /// <summary>One import at a time, so no delete is larger than one file's staging.</summary>
    private async Task DeleteFinishedStagingAsync(CancellationToken cancellationToken)
    {
        DateTime rejectionsCutoff = clock.Now.UtcDateTime - Kept;
        foreach (ImportKey import in await WithChunks().ToListAsync(cancellationToken))
        {
            await db.ImportChunks
                .Where(chunk => chunk.TenantId == import.TenantId && chunk.ImportId == import.Id)
                .ExecuteDeleteAsync(cancellationToken);
        }

        foreach (ImportKey import in await WithOldRejections(rejectionsCutoff).ToListAsync(cancellationToken))
        {
            await db.ImportRejections
                .Where(rejection => rejection.TenantId == import.TenantId && rejection.ImportId == import.Id)
                .ExecuteDeleteAsync(cancellationToken);
        }
    }

    private IQueryable<ImportKey> WithChunks() =>
        Finished()
            .Where(import => db.ImportChunks.Any(chunk => chunk.TenantId == import.TenantId && chunk.ImportId == import.Id))
            .Select(import => new ImportKey(import.TenantId, import.Id));

    private IQueryable<ImportKey> WithOldRejections(DateTime cutoff) =>
        Finished()
            .Where(import => import.CompletedAt < cutoff
                && db.ImportRejections.Any(rejection => rejection.TenantId == import.TenantId && rejection.ImportId == import.Id))
            .Select(import => new ImportKey(import.TenantId, import.Id));

    /// <summary>Imports that can no longer change: past every status that runs, and not a write that may resume.</summary>
    private IQueryable<AudienceImport> Finished() =>
        db.AudienceImports.Where(import => !AudienceImportStatusCodes.Unfinished.Contains(import.Status) && !import.Resumable);

    private IQueryable<AudienceImport> Imports(long? tenantId) =>
        tenantId is { } tenant
            ? db.AudienceImports.Where(import => import.TenantId == tenant)
            : db.AudienceImports;

    private sealed record IdleEnd(string Status, TimeSpan After, Action<AudienceImport> End);

    private sealed record IdleRule(long? TenantId, string Status, DateTime Cutoff);

    private sealed record ImportKey(long TenantId, long Id);
}

/// <summary>Runs the <see cref="ImportSweep"/> for every tenant shortly after start, then once a day, on every node.</summary>
internal sealed class ImportSweepService(IServiceScopeFactory scopes, ILogger<ImportSweepService> logger)
    : BackgroundService
{
    private static readonly TimeSpan FirstRunDelay = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan Interval = TimeSpan.FromDays(1);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            await Task.Delay(FirstRunDelay, stoppingToken);
            using PeriodicTimer timer = new(Interval);
            do
            {
                await SweepAsync(stoppingToken);
            }
            while (await timer.WaitForNextTickAsync(stoppingToken));
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // The host is stopping.
        }
    }

    /// <summary>A failed sweep is logged and tried again tomorrow; the next import start sweeps its tenant meanwhile.</summary>
    private async Task SweepAsync(CancellationToken stoppingToken)
    {
        try
        {
            await using AsyncServiceScope scope = scopes.CreateAsyncScope();
            await scope.ServiceProvider.GetRequiredService<ImportSweep>().AllTenantsAsync(stoppingToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(ex, "The daily Participant import sweep failed.");
        }
    }
}
