using Endatix.Core.Entities;
using Endatix.Infrastructure.Data.Config;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.Extensions.Logging;

namespace Endatix.Infrastructure.Data;

/// <summary>Only drafts present during the expand migration are eligible for cleanup. Older writers
/// may still create drafts afterwards. Deliberately no FK: this queue must not constrain them.</summary>
internal sealed record LegacyDraftUpgrade(long FormId, long DefinitionId);

[ApplyConfigurationFor<AppDbContext>]
internal sealed class LegacyDraftUpgradeConfiguration : IEntityTypeConfiguration<LegacyDraftUpgrade>
{
    public void Configure(EntityTypeBuilder<LegacyDraftUpgrade> builder) =>
        builder.ToTable("LegacyDraftUpgrades").HasKey(row => new { row.FormId, row.DefinitionId });
}

internal sealed partial class LegacyDraftUpgradeSeeder(AppDbContext db)
{
    internal static void Configure(DbContextOptionsBuilder options)
    {
        var previous = options.Options.FindExtension<CoreOptionsExtension>();
        options.UseSeeding((context, changed) =>
        {
            RunAsync((AppDbContext)context, CancellationToken.None).GetAwaiter().GetResult();
            previous?.Seeder?.Invoke(context, changed);
        });
        options.UseAsyncSeeding(async (context, changed, ct) =>
        {
            await RunAsync((AppDbContext)context, ct);
            if (previous?.AsyncSeeder is { } seed)
            {
                await seed(context, changed, ct);
            }
        });
    }

    private static async Task RunAsync(AppDbContext db, CancellationToken ct)
    {
        // EnsureCreated (including in-memory callers) has no migration history. A targeted
        // downgrade can also invoke EF's seeder after the upgrade queue has been dropped.
        if (!db.Database.IsRelational() || !(await db.Database.GetAppliedMigrationsAsync(ct))
            .Any(id => id.EndsWith("_NumberFormDefinitions", StringComparison.Ordinal)))
        {
            return;
        }
        var pending = db.Set<LegacyDraftUpgrade>();
        while (await pending.OrderBy(row => row.FormId).Select(row => (long?)row.FormId).FirstOrDefaultAsync(ct) is long id)
        {
            await new LegacyDraftUpgradeSeeder(db).UpgradeFormAsync(id, ct);
            await pending.Where(row => row.FormId == id).ExecuteDeleteAsync(ct);
        }
    }

    private async Task UpgradeFormAsync(long id, CancellationToken ct)
    {
        await LockLegacyRowsAsync(id, ct);
        var form = await db.Forms.IgnoreQueryFilters([EndatixQueryFilterNames.Tenant])
            .Include(row => row.ActiveDefinition).SingleOrDefaultAsync(row => row.Id == id, ct);
        if (form is null)
        {
            return;
        }
        var batch = db.Set<LegacyDraftUpgrade>().Where(row => row.FormId == id);
        var drafts = await db.FormDefinitions.IgnoreQueryFilters([EndatixQueryFilterNames.Tenant])
            .Where(row => row.FormId == id && row.TenantId == form.TenantId && row.IsDraft &&
                batch.Any(item => item.DefinitionId == row.Id))
            .OrderByDescending(row => row.ModifiedAt ?? row.CreatedAt).ThenByDescending(row => row.Id).ToListAsync(ct);
        await AdoptAsync(form, drafts, ct);
        await CleanAsync(form, drafts, ct);
        await db.SaveChangesAsync(ct);
        db.ChangeTracker.Clear();
    }

    // The EF migrator holds its transaction through seeding. Lock the actual parent and
    // definition rows: a previous binary changes them without taking any application lock.
    private async Task LockLegacyRowsAsync(long id, CancellationToken ct)
    {
        var command = db.Database.IsSqlServer()
            ? (FormattableString)$"""
              SELECT Id FROM Forms WITH (UPDLOCK, INDEX(PK_Forms)) WHERE Id = {id};
              SELECT d.Id FROM FormDefinitions d WITH (UPDLOCK, FORCESEEK, INDEX(PK_FormDefinitions))
              JOIN (SELECT DefinitionId FROM LegacyDraftUpgrades WHERE FormId = {id}
                    UNION SELECT ActiveDefinitionId FROM Forms WHERE Id = {id}) targets ON targets.DefinitionId = d.Id
              WHERE d.FormId = {id};
              """
            : $"""
              SELECT "Id" FROM "Forms" WHERE "Id" = {id} FOR UPDATE;
              SELECT d."Id" FROM "FormDefinitions" d
              WHERE d."FormId" = {id} AND (d."Id" = (SELECT "ActiveDefinitionId" FROM "Forms" WHERE "Id" = {id})
                  OR EXISTS (SELECT 1 FROM "LegacyDraftUpgrades" u WHERE u."FormId" = {id} AND u."DefinitionId" = d."Id"))
              FOR UPDATE;
              """;
        await db.Database.ExecuteSqlInterpolatedAsync(command, ct);
    }

    private async Task AdoptAsync(Form form, List<FormDefinition> drafts, CancellationToken ct)
    {
        var active = form.ActiveDefinition;
        var stray = drafts.FirstOrDefault(row => row.Id != form.ActiveDefinitionId);
        if (stray is null || (active is not null && (stray.ModifiedAt ?? stray.CreatedAt) <= (active.ModifiedAt ?? active.CreatedAt)))
        {
            return;
        }
        var head = await db.FormDrafts.IgnoreQueryFilters([EndatixQueryFilterNames.Tenant])
            .SingleOrDefaultAsync(row => row.FormId == form.Id, ct);
        if (head is null)
        {
            head = FormDraft.ForLegacyUpgrade(form);
            db.FormDrafts.Add(head);
        }
        db.FormDraftOperations.AddRange(head.Adopt(stray));
    }

    private async Task CleanAsync(Form form, List<FormDefinition> drafts, CancellationToken ct)
    {
        // Even a deleted submission can still refer to a definition.
        var bound = await db.Submissions.IgnoreQueryFilters([EndatixQueryFilterNames.Tenant, EndatixQueryFilterNames.SoftDelete])
            .Where(row => row.FormId == form.Id)
            .Select(row => row.FormDefinitionId).Distinct().ToHashSetAsync(ct);
        foreach (var row in drafts)
        {
            Clean(form, row, bound);
        }
    }

    private void Clean(Form form, FormDefinition row, HashSet<long> bound)
    {
        if (row.Id == form.ActiveDefinitionId)
        {
            form.UpdateActiveDefinitionSchema(null, false);
            var logger = db.GetService<ILoggerFactory>().CreateLogger(nameof(LegacyDraftUpgrade));
            if (logger.IsEnabled(LogLevel.Information))
            {
                LogActive(logger, form.Id, row.Id);
            }
        }
        else if (bound.Contains(row.Id))
        {
            row.UpdateDraftStatus(false);
        }
        // Keep unbound strays addressable. A by-ID writer can have read this row before
        // these locks were taken and commit its edit after they are released.
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Promoted active legacy draft {DefinitionId} for form {FormId}; requested schema compilation.")]
    private static partial void LogActive(ILogger logger, long formId, long definitionId);
}
