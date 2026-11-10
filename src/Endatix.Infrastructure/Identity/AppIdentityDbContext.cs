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

    // Users added through this context while a transaction is open: a role granted to one of them before it commits
    // (self-registration creates the user and grants the first role in one transaction) cannot be in any cache yet,
    // so it does not change the access version. A save without a transaction records nothing, so a long-lived
    // context that creates a user and later changes that user's roles still writes a version.
    private readonly HashSet<long> _addedUserIds = [];

    /// <summary>Users added through this context in the current transaction (read by tests).</summary>
    internal IReadOnlyCollection<long> UsersAddedInTransaction => _addedUserIds;

    /// <summary>
    /// Set when a save in the current transaction changed an authorization input;
    /// <see cref="AccessVersionCommitInterceptor"/> writes a new access version right before that transaction commits.
    /// </summary>
    internal bool AccessVersionChangePending { get; private set; }

    /// <summary>
    /// The one reset of the per-transaction state: when a transaction starts, after its access version is written,
    /// and when it commits or rolls back.
    /// </summary>
    internal void ResetAccessVersionState()
    {
        AccessVersionChangePending = false;
        _addedUserIds.Clear();
    }

    /// <inheritdoc/>
    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder) =>
        optionsBuilder.AddInterceptors(AccessVersionCommitInterceptor.Instance);

    /// <inheritdoc/>
    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        if (!NeedsOwnTransaction())
        {
            return base.SaveChanges(acceptAllChangesOnSuccess);
        }

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
        if (!NeedsOwnTransaction())
        {
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

    /// <summary>
    /// Applies the save-time conventions and marks an authorization change for the open transaction. True when the
    /// save changes an authorization input and no transaction is open: it then gets its own, through the execution
    /// strategy (a retrying provider requires that), so the access version commits with the change.
    /// </summary>
    private bool NeedsOwnTransaction()
    {
        if (!ProcessEntities())
        {
            return false;
        }

        if (Database.CurrentTransaction is null)
        {
            return true;
        }

        AccessVersionChangePending = true;
        return false;
    }

    /// <summary>Accepts tracked changes only when the caller asked for it, after the commit that writes the access version.</summary>
    private void AcceptChangesIf(bool acceptAllChangesOnSuccess)
    {
        if (acceptAllChangesOnSuccess)
        {
            ChangeTracker.AcceptAllChanges();
        }
    }

    /// <summary>
    /// Stamps <c>CreatedAt</c> / <c>ModifiedAt</c> and, in the same scan, tells whether this save changes an
    /// authorization input: a role, a role's permissions, a user's roles, or a user's tenant (everything
    /// <see cref="Authorization.Data.DefaultAuthorizationDataProvider"/> and the external role mapper read).
    /// Bulk <c>ExecuteUpdate</c> / <c>ExecuteDelete</c> on these tables bypass this check and must change the access version themselves.
    /// </summary>
    private bool ProcessEntities()
    {
        var inTransaction = Database.CurrentTransaction is not null;
        var inputChanged = false;
        List<long>? roleUserIds = null;
        ChangeTracker.ApplyEndatixEntityDefaults(DateTime.UtcNow, entry =>
        {
            if (entry.Entity is IdentityUserRole<long> userRole && entry.State is EntityState.Added or EntityState.Deleted or EntityState.Modified)
            {
                (roleUserIds ??= []).Add(userRole.UserId);
            }
            else if (entry.Entity is AppUser user && entry.State == EntityState.Added && inTransaction)
            {
                _addedUserIds.Add(user.Id);
            }
            else
            {
                inputChanged |= IsAuthorizationInputChange(entry);
            }
        });

        // Users and their roles can come in any order within one save, so roles are judged after the scan.
        return inputChanged || (roleUserIds?.Exists(userId => !_addedUserIds.Contains(userId)) ?? false);
    }

    /// <summary>True when this entry changes what <see cref="Authorization.Data.DefaultAuthorizationDataProvider"/> reads, user roles aside.</summary>
    private static bool IsAuthorizationInputChange(EntityEntry entry) => entry.State switch
    {
        EntityState.Added or EntityState.Deleted or EntityState.Modified when entry.Entity is RolePermission or AppRole or Permission => true,
        EntityState.Deleted => entry.Entity is AppUser,
        EntityState.Modified => entry.Entity is AppUser && TenantChanged(entry),
        _ => false
    };

    /// <summary>True only when the tenant value differs, since <c>UserManager.UpdateAsync</c> marks every column modified.</summary>
    private static bool TenantChanged(EntityEntry entry)
    {
        var tenantId = entry.Property(nameof(AppUser.TenantId));
        return tenantId.IsModified && !Equals(tenantId.OriginalValue, tenantId.CurrentValue);
    }

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
