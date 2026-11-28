using Endatix.Core.Abstractions;
using Endatix.Core.Abstractions.BackgroundJobs;
using Endatix.Core.Abstractions.Repositories;
using Endatix.Core.Entities;
using Microsoft.EntityFrameworkCore;

namespace Endatix.Infrastructure.Data.Repositories;

/// <summary>Checkpoints idle draft heads visible in the maintenance scope.</summary>
[System.Diagnostics.CodeAnalysis.Experimental("ENDATIX0007")]
public sealed class IdleDraftCheckpoints(AppDbContext db, IFormDraftRepository drafts, IDateTimeProvider clock) : IRecurringJob
{
    private sealed record Candidate(long FormId, long TenantId, long Version);

    /// <summary>Scans keys in bounded pages. Only one candidate's document is tracked at a time.</summary>
    public async Task RunAsync(CancellationToken ct)
    {
        var now = clock.UtcNow.UtcDateTime;
        long after = 0;
        while (true)
        {
            var page = await Eligible(now).Where(head => head.FormId > after).OrderBy(head => head.FormId)
                .Select(head => new Candidate(head.FormId, head.TenantId, head.Version)).Take(100).ToListAsync(ct);
            if (page.Count == 0)
            {
                return;
            }
            foreach (var candidate in page)
            {
                await CheckpointAsync(candidate, now, ct);
            }
            after = page[^1].FormId;
        }
    }

    private IQueryable<FormDraft> Eligible(DateTime now)
    {
        var cutoff = now.AddDays(-1);
        return db.FormDrafts.Where(head => head.TenantId > 0 && head.ModifiedAt <= cutoff
            && db.Forms.Any(form => form.Id == head.FormId && form.TenantId == head.TenantId)
            && !db.Set<LegacyDraftUpgrade>().Any(pending => pending.FormId == head.FormId)
            && db.FormDraftOperations.Any(row => row.FormId == head.FormId && row.TenantId == head.TenantId
                && row.Version == head.Version && row.Kind == FormDraftOperationKinds.JOURNAL));
    }

    private async Task CheckpointAsync(Candidate candidate, DateTime now, CancellationToken ct)
    {
        var head = await Eligible(now).SingleOrDefaultAsync(row => row.FormId == candidate.FormId
            && row.TenantId == candidate.TenantId && row.Version == candidate.Version, ct);
        if (head is null)
        {
            return;
        }
        var latest = await drafts.LatestSnapshotVersionAsync(head.FormId, head.Version, ct);
        if (head.CheckpointIdle(now, latest) is { } snapshot)
        {
            // An edit or another scanner after the read wins through the same version check as PATCH.
            await drafts.SaveOrConcedeAsync(head, [snapshot], ct);
        }
        db.ChangeTracker.Clear();
    }
}
