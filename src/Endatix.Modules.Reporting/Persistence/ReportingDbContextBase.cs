using Microsoft.EntityFrameworkCore;
using Endatix.Core.Abstractions;
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

    public long GetTenantId() => _tenantContext?.TenantId ?? 0;

    /// <inheritdoc />
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(ReportingPersistence.Schema);

        modelBuilder.ApplyEndatixQueryFilters(this);

        // Applied explicitly rather than by attribute scan: the shared configurations belong to every
        // derived context, and an [ApplyConfigurationFor<T>] attribute can only name one of them.
        modelBuilder.ApplyConfiguration(new ExportFormatConfiguration());
        modelBuilder.ApplyConfiguration(new FlattenedSubmissionConfiguration());
        modelBuilder.ApplyConfiguration(new FormSchemaConfiguration());
        modelBuilder.ApplyConfiguration(new SurveyTypeExportMappingConfiguration());

        // Provider specifics come second so they can refine what the shared configurations set: JSON
        // column types and filtered-index predicates, whose syntax differs.
        ApplyProviderConfigurations(modelBuilder);

        modelBuilder.ApplySnowflakeIdValueGenerators(Database);
        modelBuilder.ApplyModuleTableNames();
    }

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
