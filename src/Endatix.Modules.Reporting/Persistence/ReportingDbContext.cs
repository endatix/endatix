using Microsoft.EntityFrameworkCore;
using Ardalis.GuardClauses;
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

    public DbSet<FormExportSchema> FormExportSchemas => Set<FormExportSchema>();

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

        PrefixTableNames(modelBuilder);
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
        ProcessEntities();
        return base.SaveChanges();
    }

    public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        ProcessEntities();
        return await base.SaveChangesAsync(true, cancellationToken);
    }

    private void ProcessEntities()
    {
        var entries = ChangeTracker.Entries()
            .Where(entry => entry.State is EntityState.Added or EntityState.Modified);

        foreach (var entry in entries)
        {
            switch (entry.State)
            {
                case EntityState.Added:
                    if (entry.CurrentValues.Properties.Any(property => property.Name == "Id") &&
                        entry.CurrentValues["Id"] is long longId && longId == default)
                    {
                        entry.CurrentValues["Id"] = _idGenerator.CreateId();
                    }

                    if (entry.CurrentValues.Properties.Any(property => property.Name == "CreatedAt"))
                    {
                        var createdAt = entry.CurrentValues["CreatedAt"];
                        if (createdAt is DateTime dateTime && dateTime == default)
                        {
                            entry.CurrentValues["CreatedAt"] = DateTime.UtcNow;
                        }
                    }

                    break;
                case EntityState.Modified:
                    if (entry.CurrentValues.Properties.Any(property => property.Name == "ModifiedAt"))
                    {
                        entry.CurrentValues["ModifiedAt"] = DateTime.UtcNow;
                    }

                    break;
            }
        }
    }

    private static void PrefixTableNames(ModelBuilder builder)
    {
        Guard.Against.Null(builder);

        foreach (var entity in builder.Model.GetEntityTypes().Where(entity => !entity.IsOwned()))
        {
            builder.Entity(entity.Name).ToTable(entity.GetTableName());
        }
    }
}
