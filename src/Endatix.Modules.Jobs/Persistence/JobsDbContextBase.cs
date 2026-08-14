using Ardalis.GuardClauses;
using Endatix.Core.Abstractions;
using Endatix.Infrastructure.Data;
using Endatix.Core.Entities;
using Endatix.Core.Infrastructure.Domain;
using Endatix.Modules.Jobs.Domain;
using Endatix.Modules.Jobs.Persistence.Config;
using Microsoft.EntityFrameworkCore;

namespace Endatix.Modules.Jobs.Persistence;

/// <summary>
/// Shared model and save behaviour for the Background Jobs queue, mapped into the <c>jobs</c> schema.
/// </summary>
/// <remarks>
/// <para>
/// Provider-split by design. EF Core keeps <b>one model snapshot per context type</b>, so generating
/// PostgreSQL and SQL Server migrations against a single context makes the second generation
/// overwrite the first's snapshot — after which the next migration for the first provider diffs
/// against the wrong model and emits nonsense. A context per provider gives a snapshot per provider.
/// This is the same split endatix#813 applies to Reporting; Jobs is built with it from the start
/// rather than inheriting the problem.
/// </para>
/// <para>
/// Separate from <c>AppDbContext</c>, which means an enqueue cannot enlist in an app-schema
/// transaction. Neither shipped consumer needs it to — the webhook doorbell enqueues after its
/// business transaction has already committed, and the export endpoint has nothing to be atomic with.
/// Callers that genuinely need "a domain change and a job together" go through the outbox.
/// </para>
/// </remarks>
public abstract class JobsDbContextBase : DbContext, IJobsDbContext
{
    private readonly ITenantContext _tenantContext;
    private readonly EfCoreValueGeneratorFactory _valueGeneratorFactory;

    protected JobsDbContextBase(
        DbContextOptions options,
        ITenantContext tenantContext,
        EfCoreValueGeneratorFactory valueGeneratorFactory)
        : base(options)
    {
        _tenantContext = tenantContext;
        _valueGeneratorFactory = valueGeneratorFactory;
    }

    public DbSet<BackgroundJob> BackgroundJobs => Set<BackgroundJob>();

    /// <summary>
    /// Returns 0 outside a request, which makes the tenant query filter permissive rather than
    /// restrictive. That is intentional and load-bearing: it is how the sweeper and runner see every
    /// tenant's jobs without a tenant scope of their own.
    /// </summary>
    public long GetTenantId() => _tenantContext?.TenantId ?? 0;

    /// <inheritdoc />
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(JobsPersistence.Schema);

        modelBuilder.ApplyEndatixQueryFilters(this);

        // Applied explicitly rather than by attribute scan: the shared configuration belongs to every
        // derived context, and an [ApplyConfigurationFor<T>] attribute can only name one of them.
        modelBuilder.ApplyConfiguration(new BackgroundJobConfiguration());

        // Provider specifics come second so they can refine what the shared configuration set —
        // notably the JSON column type and the filtered-index predicates, whose syntax differs.
        ApplyProviderConfigurations(modelBuilder);

        ConfigureEntityIdValueGenerators(modelBuilder);
        PrefixTableNames(modelBuilder);
    }

    /// <summary>
    /// Applies the configurations belonging to this provider's context. Each derived context scans
    /// for its own <c>[ApplyConfigurationFor&lt;TSelf&gt;]</c> attribute, so no context can pick up
    /// another provider's mapping.
    /// </summary>
    protected abstract void ApplyProviderConfigurations(ModelBuilder modelBuilder);

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

    // Ids are assigned by an EF value generator when the entity is tracked, not here at SaveChanges.
    // That distinction matters for fan-out: AddRange of N new jobs would otherwise put N entities
    // with Id 0 into the identity map and throw before SaveChanges ever ran.
    private void ConfigureEntityIdValueGenerators(ModelBuilder builder)
    {
        var entityTypes = builder.Model.GetEntityTypes()
            .Where(entityType =>
                !entityType.IsOwned() &&
                typeof(BaseEntity).IsAssignableFrom(entityType.ClrType));

        foreach (var entityType in entityTypes)
        {
            builder.Entity(entityType.ClrType)
                .Property<long>(nameof(BaseEntity.Id))
                .HasValueGenerator((property, _) => _valueGeneratorFactory.Create<long>(property))
                .ValueGeneratedNever();
        }
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
