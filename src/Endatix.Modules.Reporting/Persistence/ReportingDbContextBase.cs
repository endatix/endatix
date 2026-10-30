using Microsoft.EntityFrameworkCore;
using Endatix.Core.Abstractions;
using Endatix.Core.Entities;
using Endatix.Modules.Reporting.Domain;
using Endatix.Modules.Reporting.Persistence.Config;
using Endatix.Infrastructure.Data;

namespace Endatix.Modules.Reporting.Persistence;

/// <summary>
/// Shared model and save behaviour for the Reporting module export read model, mapped into the
/// <c>reporting</c> schema.
/// </summary>
/// <remarks>
/// Provider-split because EF Core keeps one model snapshot per context type: a context per provider is
/// what lets each provider own its migrations without overwriting the other's snapshot.
/// </remarks>
public abstract class ReportingDbContextBase : DbContext, IReportingDbContext
{
    private const string CoreSubmissionsTable = "Submissions";

    private readonly ITenantContext _tenantContext;

    protected ReportingDbContextBase(
        DbContextOptions options,
        ITenantContext tenantContext)
        : base(options)
    {
        _tenantContext = tenantContext;
    }

    public DbSet<FormSchema> FormSchemas => Set<FormSchema>();

    public DbSet<FlattenedSubmission> FlattenedSubmissions => Set<FlattenedSubmission>();

    public DbSet<ExportFormat> ExportFormats => Set<ExportFormat>();

    public DbSet<SurveyTypeExportMapping> SurveyTypeExportMappings => Set<SurveyTypeExportMapping>();

    /// <inheritdoc />
    public DbSet<CoreSubmissionRow> CoreSubmissionRows => Set<CoreSubmissionRow>();

    /// <inheritdoc />
    public DbSet<SubmissionView> SubmissionViews => Set<SubmissionView>();

    public long GetTenantId() => _tenantContext?.TenantId ?? 0;

    /// <inheritdoc />
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        EnsureSupportedProvider();
        modelBuilder.HasDefaultSchema(ReportingPersistence.Schema);

        modelBuilder.ApplyEndatixQueryFilters(this);
        ApplySharedConfigurations(modelBuilder);

        // Provider specifics come second so they can refine what the shared configurations set: JSON
        // column types and filtered-index predicates, whose syntax differs.
        ApplyProviderConfigurations(modelBuilder);

        modelBuilder.ApplySnowflakeIdValueGenerators(Database);
        modelBuilder.ApplyModuleTableNames();

        MapCoreSubmissions(modelBuilder);
    }

    // The same name the core context resolves for Submission: its configured ToTable("Submissions"), which a table
    // prefix leaves as is.
    private void MapCoreSubmissions(ModelBuilder modelBuilder) =>
        modelBuilder.Entity<CoreSubmissionRow>()
            .HasNoKey()
            .ToTable(
                TableNamePrefix.GetTableName(typeof(Submission).FullName ?? nameof(Submission), CoreSubmissionsTable),
                CoreSchema,
                table => table.ExcludeFromMigrations());

    // Applied explicitly rather than by attribute scan: the shared configurations belong to every derived
    // context, and an [ApplyConfigurationFor<T>] attribute can only name one of them.
    private static void ApplySharedConfigurations(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfiguration(new ExportFormatConfiguration());
        modelBuilder.ApplyConfiguration(new FlattenedSubmissionConfiguration());
        modelBuilder.ApplyConfiguration(new FormSchemaConfiguration());
        modelBuilder.ApplyConfiguration(new SurveyTypeExportMappingConfiguration());
        modelBuilder.ApplyConfiguration(new SubmissionViewConfiguration());
    }

    /// <summary>
    /// Throws when the context is configured for a database provider its mappings do not target. Called first in
    /// <see cref="OnModelCreating"/>, before any shared mapping, so a wrong provider fails with a message that
    /// names it rather than at whichever mapping first uses a provider-specific feature.
    /// </summary>
    protected abstract void EnsureSupportedProvider();

    /// <summary>
    /// The schema the core tables live in on this provider. The core context sets no default schema, and EF rejects
    /// an empty one.
    /// </summary>
    protected abstract string CoreSchema { get; }

    /// <summary>
    /// Applies the configurations belonging to this provider's context. Each derived context scans for
    /// its own <c>[ApplyConfigurationFor&lt;TSelf&gt;]</c> attribute, so no context picks up another
    /// provider's mapping.
    /// </summary>
    protected abstract void ApplyProviderConfigurations(ModelBuilder modelBuilder);

    public override int SaveChanges()
    {
        ApplyEntityDefaults();
        return base.SaveChanges();
    }

    public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        ApplyEntityDefaults();
        return await base.SaveChangesAsync(true, cancellationToken);
    }

    private void ApplyEntityDefaults() =>
        ChangeTracker.ApplyEndatixEntityDefaults(DateTime.UtcNow);
}
