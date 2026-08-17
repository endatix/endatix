using Microsoft.EntityFrameworkCore;
using Endatix.Core.Abstractions;
using Endatix.Modules.Reporting.Domain;
using Endatix.Infrastructure.Data;
using Endatix.Infrastructure.Data.Abstractions;

namespace Endatix.Modules.Reporting.Persistence;

/// <summary>
/// Database context for the Reporting module export read model.
/// </summary>
public class ReportingDbContext : DbContext, ITenantDbContext
{
    private readonly IIdGenerator<long> _idGenerator;
    private readonly ITenantContext _tenantContext;

    public ReportingDbContext(
        DbContextOptions<ReportingDbContext> options,
        IIdGenerator<long> idGenerator,
        ITenantContext tenantContext)
        : base(options)
    {
        _idGenerator = idGenerator;
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
        modelBuilder.ApplyConfigurationsFor<ReportingDbContext>(typeof(ReportingDbContext).Assembly);
        ApplyProviderSpecificConfigurations(modelBuilder);

        modelBuilder.ApplyModuleTableNames();
    }

    private void ApplyProviderSpecificConfigurations(ModelBuilder builder)
    {
        string providerConfigNamespace;
        if (Database.IsNpgsql())
        {
            providerConfigNamespace = ReportingPersistence.PostgreSqlConfigNamespace;
        }
        else if (Database.IsSqlServer())
        {
            providerConfigNamespace = ReportingPersistence.SqlServerConfigNamespace;
        }
        else
        {
            throw new NotSupportedException(
                $"Database provider '{Database.ProviderName}' is not supported. " +
                $"Use Npgsql or SqlServer.");
        }

        builder.ApplyConfigurationsFromAssembly(
            typeof(ReportingDbContext).Assembly,
            type => type.Namespace == providerConfigNamespace);
    }

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
        ChangeTracker.ApplyEndatixEntityDefaults(DateTime.UtcNow, _idGenerator);
}
