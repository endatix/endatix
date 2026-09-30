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
    public DbSet<AudienceProperty> AudienceProperties => Set<AudienceProperty>();
    public DbSet<AudienceMember> AudienceMembers => Set<AudienceMember>();
    public DbSet<AudienceMembership> AudienceMemberships => Set<AudienceMembership>();
    public DbSet<AudiencePropertyValue> AudiencePropertyValues => Set<AudiencePropertyValue>();

    public long GetTenantId() => _tenantContext?.TenantId ?? 0;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(PersonalizationPersistence.Schema);
        modelBuilder.ApplyEndatixQueryFilters(this);
        modelBuilder.ApplyConfiguration(new AudienceSettingsConfiguration());
        modelBuilder.ApplyConfiguration(new AudiencePropertyConfiguration());
        modelBuilder.ApplyConfiguration(new AudienceMemberConfiguration());
        modelBuilder.ApplyConfiguration(new AudienceMembershipConfiguration());
        modelBuilder.ApplyConfiguration(new AudiencePropertyValueConfiguration());
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
