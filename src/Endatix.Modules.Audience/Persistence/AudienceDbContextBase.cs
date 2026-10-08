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

    public long GetTenantId() => _tenantContext?.TenantId ?? 0;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(AudiencePersistence.Schema);
        modelBuilder.ApplyConfiguration(new AudienceSettingsConfiguration());
        modelBuilder.ApplyConfiguration(new PropertyConfiguration());
        modelBuilder.ApplyConfiguration(new MemberConfiguration());
        modelBuilder.ApplyConfiguration(new MembershipConfiguration());
        modelBuilder.ApplyConfiguration(new PropertyValueConfiguration());
        modelBuilder.ApplyConfiguration(new AudienceImportConfiguration());
        ApplyProviderConfigurations(modelBuilder);
        modelBuilder.ApplyEndatixQueryFilters(this);
        modelBuilder.ApplyModuleTableNames();
        modelBuilder.ApplySnowflakeIdValueGenerators();
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
