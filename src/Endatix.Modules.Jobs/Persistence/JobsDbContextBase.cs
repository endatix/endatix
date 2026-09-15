using Endatix.Core.Abstractions;
using Endatix.Infrastructure.Data;
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
/// against the wrong model and emits nonsense. A context per provider gives a snapshot per provider,
/// which is why adding a provider here means adding a derived context, never reusing an existing one.
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

        modelBuilder.ApplySnowflakeIdValueGenerators(_valueGeneratorFactory);
        modelBuilder.ApplyModuleTableNames();
    }

    /// <summary>
    /// Applies the configurations belonging to this provider's context. Each derived context scans
    /// for its own <c>[ApplyConfigurationFor&lt;TSelf&gt;]</c> attribute, so no context can pick up
    /// another provider's mapping.
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
