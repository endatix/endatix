using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Endatix.Infrastructure.Data;
using Endatix.Core.Entities.Identity;
using Microsoft.AspNetCore.Identity;
using Endatix.Infrastructure.Identity.Authorization;

namespace Endatix.Infrastructure.Identity;

/// <summary>
/// This class integrates the AspNetCore EntityFramework Identity DB context, which also defines the Endatix specific details such as configs options and schema options
/// </summary>
public class AppIdentityDbContext : IdentityDbContext<AppUser, AppRole, long>
{
    public AppIdentityDbContext(DbContextOptions<AppIdentityDbContext> options) : base(options)
    {
    }

    public DbSet<Permission> Permissions { get; set; }
    public DbSet<RolePermission> RolePermissions { get; set; }
    public DbSet<EmailVerificationToken> EmailVerificationTokens { get; set; }
    public DbSet<AuthorizationStamp> AuthorizationStamps { get; set; }

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.ApplyConfigurationsFor<AppIdentityDbContext>(Endatix.Infrastructure.AssemblyReference.Assembly);

        builder.HasDefaultSchema("identity");

        builder.ApplySnowflakeIdValueGenerators(Database);

        RenameIdentityTables(builder);
    }

    /// <summary>
    /// Set when a save in the current transaction changed an authorization input;
    /// <see cref="AuthorizationStampCommitInterceptor"/> writes a new stamp right before that transaction commits.
    /// </summary>
    internal bool AuthorizationStampChangePending { get; set; }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder) =>
        optionsBuilder.AddInterceptors(AuthorizationStampCommitInterceptor.Instance);

    public override int SaveChanges()
    {
        ProcessEntities();
        if (!AuthorizationInputsChanged())
        {
            return base.SaveChanges();
        }

        AuthorizationStampChangePending = true;
        if (Database.CurrentTransaction is not null)
        {
            return base.SaveChanges();
        }

        // A save that would run without a transaction (EF skips one for a single statement) gets one, so the
        // stamp commits with the change.
        using var transaction = Database.BeginTransaction();
        var saved = base.SaveChanges();
        transaction.Commit();
        return saved;
    }

    /// <inheritdoc/>
    public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        ProcessEntities();
        if (!AuthorizationInputsChanged())
        {
            return await base.SaveChangesAsync(true, cancellationToken);
        }

        AuthorizationStampChangePending = true;
        if (Database.CurrentTransaction is not null)
        {
            return await base.SaveChangesAsync(true, cancellationToken);
        }

        await using var transaction = await Database.BeginTransactionAsync(cancellationToken);
        var saved = await base.SaveChangesAsync(true, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return saved;
    }

    /// <summary>
    /// A role, a role's permissions, a user's roles, or a user's tenant changes in this save: everything
    /// <see cref="Authorization.Data.DefaultAuthorizationDataProvider"/> and the external role mapper read.
    /// Bulk <c>ExecuteUpdate</c> / <c>ExecuteDelete</c> on these tables bypass this check and must change the stamp themselves.
    /// </summary>
    private bool AuthorizationInputsChanged() =>
        ChangeTracker.Entries().Any(entry => entry.State switch
        {
            EntityState.Added or EntityState.Deleted => entry.Entity is IdentityUserRole<long> or RolePermission or AppRole or Permission
                || (entry.State == EntityState.Deleted && entry.Entity is AppUser),
            EntityState.Modified => entry.Entity is IdentityUserRole<long> or RolePermission or AppRole or Permission
                || (entry.Entity is AppUser && TenantChanged(entry)),
            _ => false
        });

    // UserManager.UpdateAsync marks every column modified, so compare values: a profile save or a sign-in
    // must not change the stamp.
    private static bool TenantChanged(EntityEntry entry)
    {
        var tenantId = entry.Property(nameof(AppUser.TenantId));
        return tenantId.IsModified && !Equals(tenantId.OriginalValue, tenantId.CurrentValue);
    }

    private void ProcessEntities() =>
        ChangeTracker.ApplyEndatixEntityDefaults(DateTime.UtcNow);

    private void RenameIdentityTables(ModelBuilder builder)
    {
        builder.Entity<AppUser>().ToTable("Users");
        builder.Entity<AppRole>().ToTable("Roles");
        builder.Entity<IdentityUserClaim<long>>().ToTable("UserClaims");
        builder.Entity<IdentityUserRole<long>>().ToTable("UserRoles");
        builder.Entity<IdentityUserLogin<long>>().ToTable("UserLogins");
        builder.Entity<IdentityRoleClaim<long>>().ToTable("RoleClaims");
        builder.Entity<IdentityUserToken<long>>().ToTable("UserTokens");
    }
}