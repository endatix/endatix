using Endatix.Core.Entities.Identity;
using Endatix.Infrastructure.Data;
using Endatix.Infrastructure.Identity.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;

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
    /// <summary>Access versions, one row per tenant; see <see cref="AccessVersion"/>.</summary>
    internal DbSet<AccessVersion> AccessVersions => Set<AccessVersion>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.ApplyConfigurationsFor<AppIdentityDbContext>(Endatix.Infrastructure.AssemblyReference.Assembly);

        builder.HasDefaultSchema("identity");

        builder.ApplySnowflakeIdValueGenerators(Database);

        RenameIdentityTables(builder);
    }

    // Users added through this context in the current transaction: a role granted to one of them before it commits
    // (self-registration creates the user and grants the role in one transaction) cannot be in any cache yet, so it
    // does not change the access version. Cleared when a transaction commits or rolls back.
    private readonly HashSet<long> _addedUserIds = [];

    /// <summary>Users added through this context in the current transaction (read by tests).</summary>
    internal IReadOnlyCollection<long> UsersAddedInTransaction => _addedUserIds;

    /// <summary>Clears the per-transaction state when a transaction commits or rolls back.</summary>
    internal void EndTransaction()
    {
        AccessVersionChangePending = false;
        _addedUserIds.Clear();
    }

    /// <summary>
    /// Set when a save in the current transaction changed an authorization input;
    /// <see cref="AccessVersionCommitInterceptor"/> writes a new access version right before that transaction commits,
    /// and clears it when a transaction starts or rolls back.
    /// </summary>
    internal bool AccessVersionChangePending { get; set; }

    /// <inheritdoc/>
    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder) =>
        optionsBuilder.AddInterceptors(AccessVersionCommitInterceptor.Instance);

    /// <inheritdoc/>
    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        ProcessEntities();
        if (!AuthorizationInputsChanged())
        {
            return base.SaveChanges(acceptAllChangesOnSuccess);
        }

        if (Database.CurrentTransaction is not null)
        {
            AccessVersionChangePending = true;
            return base.SaveChanges(acceptAllChangesOnSuccess);
        }

        // A save that would run without a transaction (EF skips one for a single statement) gets one, so the
        // access version commits with the change. It runs through the execution strategy, which a retrying provider
        // requires for a transaction it did not open, and accepts changes only after the commit that writes the access version.
        var saved = Database.CreateExecutionStrategy().Execute(() =>
        {
            using var transaction = Database.BeginTransaction();
            AccessVersionChangePending = true;
            var rows = base.SaveChanges(acceptAllChangesOnSuccess: false);
            transaction.Commit();
            return rows;
        });
        AcceptChangesIf(acceptAllChangesOnSuccess);
        return saved;
    }

    /// <inheritdoc/>
    public override async Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
    {
        ProcessEntities();
        if (!AuthorizationInputsChanged())
        {
            return await base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
        }

        if (Database.CurrentTransaction is not null)
        {
            AccessVersionChangePending = true;
            return await base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
        }

        var saved = await Database.CreateExecutionStrategy().ExecuteAsync(
            async ct =>
            {
                await using var transaction = await Database.BeginTransactionAsync(ct);
                AccessVersionChangePending = true;
                var rows = await base.SaveChangesAsync(acceptAllChangesOnSuccess: false, ct);
                await transaction.CommitAsync(ct);
                return rows;
            },
            cancellationToken);
        AcceptChangesIf(acceptAllChangesOnSuccess);
        return saved;
    }

    private void AcceptChangesIf(bool acceptAllChangesOnSuccess)
    {
        if (acceptAllChangesOnSuccess)
        {
            ChangeTracker.AcceptAllChanges();
        }
    }

    /// <summary>
    /// A role, a role's permissions, a user's roles, or a user's tenant changes in this save: everything
    /// <see cref="Authorization.Data.DefaultAuthorizationDataProvider"/> and the external role mapper read.
    /// Bulk <c>ExecuteUpdate</c> / <c>ExecuteDelete</c> on these tables bypass this check and must change the access version themselves.
    /// </summary>
    private bool AuthorizationInputsChanged()
    {
        var entries = ChangeTracker.Entries().ToList();
        foreach (var added in entries.Where(entry => entry.State == EntityState.Added && entry.Entity is AppUser))
        {
            _addedUserIds.Add(((AppUser)added.Entity).Id);
        }

        return entries.Any(IsAuthorizationInputChange);
    }

    private bool IsAuthorizationInputChange(EntityEntry entry) => entry.State switch
    {
        EntityState.Added or EntityState.Deleted or EntityState.Modified when entry.Entity is IdentityUserRole<long> userRole =>
            !_addedUserIds.Contains(userRole.UserId),
        EntityState.Added or EntityState.Deleted or EntityState.Modified => entry.Entity is RolePermission or AppRole or Permission
            || (entry.State == EntityState.Deleted && entry.Entity is AppUser)
            || (entry.State == EntityState.Modified && entry.Entity is AppUser && TenantChanged(entry)),
        _ => false
    };

    // UserManager.UpdateAsync marks every column modified, so compare values: a profile save or a sign-in
    // must not change the access version.
    private static bool TenantChanged(EntityEntry entry)
    {
        var tenantId = entry.Property(nameof(AppUser.TenantId));
        return tenantId.IsModified && !Equals(tenantId.OriginalValue, tenantId.CurrentValue);
    }

    private void ProcessEntities() =>
        ChangeTracker.ApplyEndatixEntityDefaults(DateTime.UtcNow);

    private static void RenameIdentityTables(ModelBuilder builder)
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
