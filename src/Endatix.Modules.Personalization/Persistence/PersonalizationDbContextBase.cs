using Endatix.Core.Abstractions;
using Endatix.Infrastructure.Data;
using Endatix.Modules.Personalization.Domain;
using Endatix.Modules.Personalization.Persistence.Config;
using Microsoft.EntityFrameworkCore;

namespace Endatix.Modules.Personalization.Persistence;

/// <summary>
/// Shared model for the Personalization schema.
/// </summary>
public abstract class PersonalizationDbContextBase : DbContext, IPersonalizationDbContext
{
    private readonly ITenantContext _tenantContext;

    protected PersonalizationDbContextBase(
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

    public long GetTenantId() => _tenantContext?.TenantId ?? 0;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(PersonalizationPersistence.Schema);
        modelBuilder.ApplyEndatixQueryFilters(this);
        modelBuilder.ApplyConfiguration(new AudienceSettingsConfiguration());
        modelBuilder.ApplyConfiguration(new PropertyConfiguration());
        modelBuilder.ApplyConfiguration(new MemberConfiguration());
        modelBuilder.ApplyConfiguration(new MembershipConfiguration());
        modelBuilder.ApplyConfiguration(new PropertyValueConfiguration());
        ApplyProviderConfigurations(modelBuilder);
        modelBuilder.ApplySnowflakeIdValueGenerators();
        modelBuilder.ApplyModuleTableNames();
    }

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
