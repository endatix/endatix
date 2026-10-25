using Endatix.Core.Abstractions;
using Endatix.Modules.Audience.Domain;
using Endatix.Modules.Audience.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Endatix.Modules.Audience.Features.Imports;

/// <summary>
/// Ends imports nobody is moving and deletes what finished imports staged: an upload or a checked import idle a day
/// expires, a Check stalled six hours fails, a write stalled six hours fails resumable (the state a dead-lettered or
/// hung write job leaves), finished imports lose their chunks, and their rejections go a week after they finish. A
/// write that may resume keeps both. Runs for a tenant when it starts an import, and for every tenant once a day.
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
    public static readonly TimeSpan RejectionsKept = TimeSpan.FromDays(7);

    internal const string CheckStalled = "The check stopped before it finished. Start the import again.";
    internal const string WriteStalled = "The write stopped before it finished. Resume it to continue where it stopped.";

    private const int Batch = 100;

    public Task ForTenantAsync(long tenantId, CancellationToken cancellationToken) =>
        SweepAsync(tenantId, cancellationToken);

    public Task AllTenantsAsync(CancellationToken cancellationToken) => SweepAsync(null, cancellationToken);

    private async Task SweepAsync(long? tenantId, CancellationToken cancellationToken)
    {
        DateTime now = clock.Now.UtcDateTime;
        await EndIdleAsync(new IdleRule(tenantId, AudienceImportStatusCodes.Uploading, now - IdleLimit), import => import.Expire(), cancellationToken);
        await EndIdleAsync(new IdleRule(tenantId, AudienceImportStatusCodes.Checked, now - IdleLimit), import => import.Expire(), cancellationToken);
        await EndIdleAsync(new IdleRule(tenantId, AudienceImportStatusCodes.Checking, now - StallLimit), import => import.Fail(CheckStalled), cancellationToken);
        await EndIdleAsync(new IdleRule(tenantId, AudienceImportStatusCodes.Writing, now - StallLimit), import => import.Fail(WriteStalled, resumable: true), cancellationToken);
        await DeleteFinishedChunksAsync(tenantId, cancellationToken);
        await DeleteOldRejectionsAsync(tenantId, now - RejectionsKept, cancellationToken);
    }

    /// <summary>Ends, in batches, every import in the rule's status with no change since its cutoff.</summary>
    private async Task EndIdleAsync(IdleRule rule, Action<AudienceImport> end, CancellationToken cancellationToken)
    {
        List<AudienceImport> idle;
        do
        {
            idle = await Idle(rule).Take(Batch).ToListAsync(cancellationToken);
            foreach (AudienceImport import in idle)
            {
                end(import);
                await SaveUnlessMovedAsync(import, cancellationToken);
            }

            ((DbContext)db).ChangeTracker.Clear();
        }
        while (idle.Count == Batch);
    }

    /// <summary>An upload's chunks count as activity: sending one changes no import column.</summary>
    private IQueryable<AudienceImport> Idle(IdleRule rule) =>
        Imports(rule.TenantId).Where(import => import.Status == rule.Status
            && (import.ModifiedAt ?? import.CreatedAt) < rule.Cutoff
            && !db.ImportChunks.Any(chunk => chunk.TenantId == import.TenantId
                && chunk.ImportId == import.Id
                && chunk.CreatedAt >= rule.Cutoff));

    /// <summary>Saves the transition; a run or another sweep that moved the import first wins.</summary>
    private async Task SaveUnlessMovedAsync(AudienceImport import, CancellationToken cancellationToken)
    {
        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            ((DbContext)db).Entry(import).State = EntityState.Detached;
        }
    }

    private async Task DeleteFinishedChunksAsync(long? tenantId, CancellationToken cancellationToken)
    {
        IQueryable<AudienceImport> finished = Finished(tenantId);
        await db.ImportChunks
            .Where(chunk => finished.Any(import => import.TenantId == chunk.TenantId && import.Id == chunk.ImportId))
            .ExecuteDeleteAsync(cancellationToken);
    }

    /// <summary>One import at a time, so a delete is never larger than one file's rejected rows.</summary>
    private async Task DeleteOldRejectionsAsync(long? tenantId, DateTime cutoff, CancellationToken cancellationToken)
    {
        List<long> importIds = await Finished(tenantId)
            .Where(import => import.CompletedAt < cutoff
                && db.ImportRejections.Any(rejection => rejection.TenantId == import.TenantId && rejection.ImportId == import.Id))
            .Select(import => import.Id)
            .ToListAsync(cancellationToken);
        foreach (long importId in importIds)
        {
            await db.ImportRejections
                .Where(rejection => rejection.ImportId == importId && (tenantId == null || rejection.TenantId == tenantId))
                .ExecuteDeleteAsync(cancellationToken);
        }
    }

    /// <summary>Imports that can no longer change: past every status that runs, and not a write that may resume.</summary>
    private IQueryable<AudienceImport> Finished(long? tenantId) =>
        Imports(tenantId).Where(import => !AudienceImportStatusCodes.Unfinished.Contains(import.Status) && !import.Resumable);

    private IQueryable<AudienceImport> Imports(long? tenantId) =>
        tenantId is { } tenant
            ? db.AudienceImports.Where(import => import.TenantId == tenant)
            : db.AudienceImports;

    private sealed record IdleRule(long? TenantId, string Status, DateTime Cutoff);
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
