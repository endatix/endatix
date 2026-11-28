using System.Collections.Frozen;
using Endatix.Core.Abstractions;
using Endatix.Core.Abstractions.BackgroundJobs;
using Endatix.Core.Abstractions.Forms;
using Endatix.Core.Abstractions.Repositories;
using Endatix.Core.Entities;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace Endatix.Infrastructure.Data.Repositories;

/// <summary>Checkpoints idle draft heads visible in the maintenance scope.</summary>
internal sealed class IdleDraftCheckpoints(IdleDraftStore store, IDateTimeProvider clock, ILogger<IdleDraftCheckpoints> logger) : IRecurringJob
{
    /// <summary>Scans keys in bounded pages. Only one candidate's document is tracked at a time.</summary>
    public async Task RunAsync(CancellationToken cancellationToken)
    {
        var now = clock.UtcNow.UtcDateTime;
        long after = 0;
        while (true)
        {
            var page = await store.ReadPageAsync(after, now, cancellationToken);
            if (page.Count == 0)
            {
                return;
            }
            foreach (var candidate in page)
            {
                await CheckpointOrLogAsync(candidate, now, cancellationToken);
            }
            after = page[^1].FormId;
        }
    }

    private async Task CheckpointOrLogAsync(IdleDraftStore.Candidate candidate, DateTime now, CancellationToken ct)
    {
        if (await store.TryCheckpointAsync(candidate, now, ct) is { } code)
        {
            // Provider messages may quote the rejected row. Log only its identity and diagnostic code.
            logger.LogError("Idle checkpoint rejected for tenant {TenantId}, form {FormId}, version {Version} (database code {Code})",
                candidate.TenantId, candidate.FormId, candidate.Version, code);
        }
    }
}

internal sealed class IdleDraftStore(AppDbContext db, IFormDraftRepository drafts, IFormSubmissionsLock formLock)
{
    internal sealed record Candidate(long FormId, long TenantId, long Version);

    internal Task<List<Candidate>> ReadPageAsync(long after, DateTime now, CancellationToken ct) =>
        Eligible(now).Where(head => head.FormId > after).OrderBy(head => head.FormId)
            .Select(head => new Candidate(head.FormId, head.TenantId, head.Version)).Take(100).ToListAsync(ct);

    private IQueryable<FormDraft> Eligible(DateTime now)
    {
        var cutoff = now.AddDays(-1);
        return db.FormDrafts.Where(head => head.TenantId > 0 && head.ModifiedAt <= cutoff
            && db.Forms.Any(form => form.Id == head.FormId && form.TenantId == head.TenantId)
            && !db.Set<LegacyDraftUpgrade>().Any(pending => pending.FormId == head.FormId)
            && db.FormDraftOperations.Any(row => row.FormId == head.FormId && row.TenantId == head.TenantId
                && row.Version == head.Version && row.Kind == FormDraftOperationKinds.JOURNAL));
    }

    internal async Task<string?> TryCheckpointAsync(Candidate candidate, DateTime now, CancellationToken ct)
    {
        try
        {
            await CheckpointAsync(candidate, now, ct);
            return null;
        }
        catch (Exception error) when (RowRejectionCode(error) is { } code)
        {
            ct.ThrowIfCancellationRequested();
            return code;
        }
    }

    private static readonly FrozenSet<string> _postgresRowErrors = [
        PostgresErrorCodes.CheckViolation, PostgresErrorCodes.ForeignKeyViolation, PostgresErrorCodes.NotNullViolation,
        PostgresErrorCodes.StringDataRightTruncation, PostgresErrorCodes.InvalidTextRepresentation, PostgresErrorCodes.NumericValueOutOfRange
    ];

    // Constraint, NULL, truncated text, malformed JSON, or numeric overflow in the candidate write.
    private static readonly FrozenSet<int> _sqlServerRowErrors = [547, 515, 2628, 8152, 13609, 8115];

    private static string? RowRejectionCode(Exception? cause) => cause switch
    {
        DbUpdateException update => RowRejectionCode(update.InnerException),
        PostgresException pg when _postgresRowErrors.Contains(pg.SqlState) => pg.SqlState,
        SqlException sql when _sqlServerRowErrors.Contains(sql.Number) =>
            sql.Number.ToString(System.Globalization.CultureInfo.InvariantCulture),
        _ => null
    };

    private async Task CheckpointAsync(Candidate candidate, DateTime now, CancellationToken ct)
    {
        // Form deletion takes the exclusive side. This shared section still lets draft writers race normally.
        var locked = await formLock.BeginAddingAsync(candidate.FormId, ct);
        if (!locked.IsSuccess)
        {
            return;
        }
        await using var section = locked.Value;
        if (await SaveEligibleAsync(candidate, now, ct))
        {
            await section.CommitAsync(ct);
            db.ChangeTracker.Clear();
        }
    }

    private async Task<bool> SaveEligibleAsync(Candidate candidate, DateTime now, CancellationToken ct)
    {
        var head = await Eligible(now).SingleOrDefaultAsync(row => row.FormId == candidate.FormId
            && row.TenantId == candidate.TenantId && row.Version == candidate.Version, ct);
        if (head is null)
        {
            return false;
        }
        return head.CheckpointIdleJournalTail(now) is { } snapshot && await drafts.SaveOrConcedeAsync(head, [snapshot], ct);
    }
}
