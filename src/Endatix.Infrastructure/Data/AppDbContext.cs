using System.Linq.Expressions;
using System.Reflection;
using Microsoft.EntityFrameworkCore;
using Endatix.Core.Entities;
using Endatix.Core.Abstractions;
using Ardalis.GuardClauses;
using Microsoft.EntityFrameworkCore.Metadata;

namespace Endatix.Infrastructure.Data;

/// <summary>
/// Represents the application database context for persisting the Endatix Domain entities
/// </summary>
public class AppDbContext : DbContext
{
    private readonly IIdGenerator<long> _idGenerator;
    private readonly ITenantContext _tenantContext;

    protected AppDbContext() { }
    public AppDbContext(DbContextOptions<AppDbContext> options, IIdGenerator<long> idGenerator, ITenantContext tenantContext) : base(options)
    {
        _idGenerator = idGenerator;
        _tenantContext = tenantContext;
    }

    public DbSet<Form> Forms { get; set; }

    public DbSet<FormDefinition> FormDefinitions { get; set; }

    public DbSet<FormTemplate> FormTemplates { get; set; }

    public DbSet<Submission> Submissions { get; set; }

    public DbSet<Theme> Themes { get; set; }

    public DbSet<CustomQuestion> CustomQuestions { get; set; }

    public DbSet<SubmissionExportRow> SubmissionExportRows { get; set; }

    public DbSet<EmailTemplate> EmailTemplates { get; set; }

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        var getTenantIdMethod = typeof(AppDbContext).GetMethod(nameof(GetTenantId), BindingFlags.NonPublic | BindingFlags.Instance)!;
        var currentTenantId = Expression.Call(Expression.Constant(this), getTenantIdMethod);
        foreach (var entityType in builder.Model.GetEntityTypes())
        {
            var parameter = Expression.Parameter(entityType.ClrType, "e");
            var filters = new List<Expression>();

            // Add deletion filter for BaseEntity descendants
            if (typeof(BaseEntity).IsAssignableFrom(entityType.ClrType))
            {
                var isDeletedProperty = Expression.Property(parameter, "IsDeleted");
                var isDeletedFilter = Expression.Equal(isDeletedProperty, Expression.Constant(false));
                filters.Add(isDeletedFilter);
            }

            // Add tenant filter for TenantEntity descendants
            if (typeof(TenantEntity).IsAssignableFrom(entityType.ClrType))
            {
                var currentTenantIdIsZero = Expression.Equal(Expression.Convert(currentTenantId, typeof(long)), Expression.Constant(0L));
                var tenantIdProperty = Expression.Property(parameter, "TenantId");
                var tenantIdEquals = Expression.Equal(tenantIdProperty, Expression.Convert(currentTenantId, typeof(long)));
                var tenantFilter = Expression.OrElse(currentTenantIdIsZero, tenantIdEquals);
                filters.Add(tenantFilter);
            }

            if (filters.Any())
            {
                var combinedFilter = filters.Aggregate(Expression.AndAlso);
                var lambda = Expression.Lambda(combinedFilter, parameter);
                builder.Entity(entityType.ClrType).HasQueryFilter(lambda);
            }
        }

        builder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
        builder.Entity<SubmissionExportRow>()
            .HasNoKey()
            .ToTable(t => t.ExcludeFromMigrations());
        PrefixTableNames(builder);
    }

    private long GetTenantId()
    {
        return _tenantContext?.TenantId ?? 0;
    }

    public override int SaveChanges()
    {
        ProcessEntities();
        return base.SaveChanges();
    }

    /// <inheritdoc/>
    public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        ProcessEntities();
        return await base.SaveChangesAsync(true, cancellationToken);
    }

    private void ProcessEntities()
    {
        var entries = ChangeTracker.Entries()
            .Where(e => e.State == EntityState.Added || e.State == EntityState.Modified);

        foreach (var entry in entries)
        {
            switch (entry.State)
            {
                case EntityState.Added:
                    // Generate an id if necessary
                    if (entry.CurrentValues.Properties.Any(p => p.Name == "Id") &&
                        entry.CurrentValues["Id"] is default(long))
                    {
                        entry.CurrentValues["Id"] = _idGenerator.CreateId();
                    }

                    // Set the CreatedAt value
                    if (entry.CurrentValues.Properties.Any(p => p.Name == "CreatedAt"))
                    {
                        entry.CurrentValues["CreatedAt"] = DateTime.UtcNow;
                    }

                    break;
                case EntityState.Modified:
                    // Set the ModifiedAt value
                    if (entry.CurrentValues.Properties.Any(p => p.Name == "ModifiedAt"))
                    {
                        entry.CurrentValues["ModifiedAt"] = DateTime.UtcNow;
                    }

                    break;
            }
        }
    }

    private void PrefixTableNames(ModelBuilder builder)
    {
        Guard.Against.Null(builder);
        var entityTypes = builder.Model.GetEntityTypes();
        if (entityTypes is null || !entityTypes.Any())
        {
            return;
        }

        foreach (var entity in entityTypes)
        {
            if (ShouldPrefixTable(entity))
            {
                builder.Entity(entity.Name).ToTable(TableNamePrefix.GetTableName(entity.Name));
            }
        }
    }

    private bool ShouldPrefixTable(IMutableEntityType? entityType)
    {
        Guard.Against.Null(entityType);

        if (entityType.IsOwned())
        {
            return false;
        }

        return true;
    }
}
