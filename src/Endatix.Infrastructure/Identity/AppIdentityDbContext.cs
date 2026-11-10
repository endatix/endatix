using Endatix.Core.Entities.Identity;
using Endatix.Infrastructure.Data;
using Endatix.Infrastructure.Identity.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

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

        DeclareAccessInputs(builder);
    }

    /// <summary>
    /// The entity types whose rows decide a user's authorization data (everything
    /// <see cref="Authorization.Data.DefaultAuthorizationDataProvider"/> and the external role mapper read). A save that
    /// changes one moves an access version (<see cref="AccessInputChanges"/>); bulk writes over them are forbidden.
    /// </summary>
    private static void DeclareAccessInputs(ModelBuilder builder)
    {
        builder.Entity<AppRole>().IsAccessInput();
        builder.Entity<AppUser>().IsAccessInput();
        builder.Entity<IdentityUserRole<long>>().IsAccessInput();
        builder.Entity<RolePermission>().IsAccessInput();
        builder.Entity<Permission>().IsAccessInput();
    }

    // Users added through this context while a transaction is open: a role granted to one of them before it commits
    // (self-registration creates the user and grants the first role in one transaction) cannot be in any cache yet,
    // so it does not change the access version. A save without a transaction records nothing, so a long-lived
    // context that creates a user and later changes that user's roles still writes a version.
    private readonly HashSet<long> _addedUserIds = [];

    /// <summary>Users added through this context in the current transaction (read by tests).</summary>
    internal IReadOnlyCollection<long> UsersAddedInTransaction => _addedUserIds;

    // The access version rows that saves in the current transaction moved.
    private readonly HashSet<long> _pendingAccessRows = [];

    /// <summary>
    /// The access version rows (tenant ids, 0 for system roles) that saves in the current transaction changed an input
    /// of; <see cref="AccessVersionCommitInterceptor"/> writes them right before that transaction commits.
    /// </summary>
    internal IReadOnlyCollection<long> PendingAccessVersionRows => _pendingAccessRows;

    /// <summary>
    /// The one reset of the per-transaction state: when a transaction starts, after its access versions are written,
    /// and when it commits or rolls back.
    /// </summary>
    internal void ResetAccessVersionState()
    {
        _pendingAccessRows.Clear();
        _addedUserIds.Clear();
    }

    /// <inheritdoc/>
    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder) =>
        optionsBuilder.AddInterceptors(AccessVersionCommitInterceptor.Instance);

    /// <inheritdoc/>
    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        var accessRows = CollectAccessInputChanges().Resolve(this);
        if (!NeedsOwnTransaction(accessRows))
        {
            return base.SaveChanges(acceptAllChangesOnSuccess);
        }

        var saved = Database.CreateExecutionStrategy().Execute(() =>
        {
            using var transaction = Database.BeginTransaction();
            _pendingAccessRows.UnionWith(accessRows);
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
        var accessRows = await CollectAccessInputChanges().ResolveAsync(this, cancellationToken);
        if (!NeedsOwnTransaction(accessRows))
        {
            return await base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
        }

        var saved = await Database.CreateExecutionStrategy().ExecuteAsync(async ct =>
            {
                await using var transaction = await Database.BeginTransactionAsync(ct);
                _pendingAccessRows.UnionWith(accessRows);
                var rows = await base.SaveChangesAsync(acceptAllChangesOnSuccess: false, ct);
                await transaction.CommitAsync(ct);
                return rows;
            },
            cancellationToken);
        AcceptChangesIf(acceptAllChangesOnSuccess);
        return saved;
    }

    /// <summary>
    /// Marks the access version rows a save moves for the open transaction. True when the save moves one and no
    /// transaction is open: it then gets its own, through the execution strategy (a retrying provider requires that),
    /// so the access versions commit with the change.
    /// </summary>
    private bool NeedsOwnTransaction(IReadOnlyCollection<long> rows)
    {
        if (rows.Count == 0)
        {
            return false;
        }

        if (Database.CurrentTransaction is null)
        {
            return true;
        }

        _pendingAccessRows.UnionWith(rows);
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
    /// Stamps <c>CreatedAt</c> / <c>ModifiedAt</c> and, in the same scan, collects the access inputs this save changes.
    /// Bulk <c>ExecuteUpdate</c> / <c>ExecuteDelete</c> on these tables would bypass it, so none may run
    /// (<c>AccessInputBulkWriteTests</c>).
    /// </summary>
    private AccessInputChanges CollectAccessInputChanges()
    {
        var inTransaction = Database.CurrentTransaction is not null;
        AccessInputChanges changes = new(_addedUserIds);
        ChangeTracker.ApplyEndatixEntityDefaults(DateTime.UtcNow, entry =>
        {
            if (inTransaction && entry is { Entity: AppUser user, State: EntityState.Added })
            {
                _addedUserIds.Add(user.Id);
            }

            changes.Add(entry);
        });

        return changes;
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
