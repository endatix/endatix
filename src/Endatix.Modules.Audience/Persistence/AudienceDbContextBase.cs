using Endatix.Core.Abstractions;
using Endatix.Infrastructure.Data;
using Endatix.Modules.Audience.Domain;
using Endatix.Modules.Audience.Persistence.Config;
using Microsoft.EntityFrameworkCore;

namespace Endatix.Modules.Audience.Persistence;

/// <summary>
/// Shared model for the audience schema.
/// </summary>
public abstract class AudienceDbContextBase : DbContext, IAudienceDbContext
{
    private readonly ITenantContext _tenantContext;

    protected AudienceDbContextBase(
        DbContextOptions options,
        ITenantContext tenantContext)
        : base(options)
    {
        _tenantContext = tenantContext;
    }

    public DbSet<AudienceSettings> AudienceSettings => Set<AudienceSettings>();
    public DbSet<Property> Properties => Set<Property>();
    public DbSet<Member> Members => Set<Member>();
    public DbSet<Membership> Memberships => Set<Membership>();
    public DbSet<PropertyValue> PropertyValues => Set<PropertyValue>();
    public DbSet<AudienceImport> AudienceImports => Set<AudienceImport>();
    public DbSet<AudienceLink> Links => Set<AudienceLink>();

    public long GetTenantId() => _tenantContext?.TenantId ?? 0;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        EnsureSupportedProvider();
        modelBuilder.HasDefaultSchema(AudiencePersistence.Schema);
        modelBuilder.ApplyConfiguration(new AudienceSettingsConfiguration(SqlDialect));
        modelBuilder.ApplyConfiguration(new PropertyConfiguration(SqlDialect));
        modelBuilder.ApplyConfiguration(new MemberConfiguration(SqlDialect));
        modelBuilder.ApplyConfiguration(new MembershipConfiguration(SqlDialect));
        modelBuilder.ApplyConfiguration(new PropertyValueConfiguration(SqlDialect));
        modelBuilder.ApplyConfiguration(new AudienceImportConfiguration(SqlDialect));
        modelBuilder.ApplyConfiguration(new AudienceLinkConfiguration(SqlDialect));
        modelBuilder.ApplyEndatixQueryFilters(this);
        modelBuilder.ApplyModuleTableNames();
        modelBuilder.ApplySnowflakeIdValueGenerators(Database);
    }

    /// <summary>
    /// Throws when the context is configured for a database provider its mappings do not target. Called first in
    /// <see cref="OnModelCreating"/>, before any shared mapping, so a wrong provider fails with a message that
    /// names it rather than at whichever mapping first uses a provider-specific feature.
    /// </summary>
    protected abstract void EnsureSupportedProvider();

    /// <summary>
    /// How this context's provider spells the SQL fragments of the shared mapping. The mapping itself is the same on
    /// every provider.
    /// </summary>
    private protected abstract AudienceSqlDialect SqlDialect { get; }

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
