using Microsoft.EntityFrameworkCore;
using Endatix.Core.Entities;
using Endatix.Core.Abstractions;
using Ardalis.GuardClauses;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.AspNetCore.Http;
using FastEndpoints.Security;

namespace Endatix.Infrastructure.Data;

/// <summary>
/// Represents the application database context for persisting the Endatix Domain entities
/// </summary>
public class AppDbContext : DbContext
{
    private readonly IIdGenerator<long> _idGenerator;
    private readonly IHttpContextAccessor _httpContextAccessor;

    protected AppDbContext() { }
    public AppDbContext(DbContextOptions<AppDbContext> options, IIdGenerator<long> idGenerator, IHttpContextAccessor httpContextAccessor) : base(options)
    {
        _idGenerator = idGenerator;
        _httpContextAccessor = httpContextAccessor;
    }

    public DbSet<Form> Forms { get; set; }

    public DbSet<FormDefinition> FormDefinitions { get; set; }

    public DbSet<Submission> Submissions { get; set; }

    private bool IsInternalUser() {
        var emailAddress = _httpContextAccessor.HttpContext?.User.ClaimValue("email");
        if(emailAddress != null && emailAddress.ToLower().EndsWith("@endatix.com")) {
            return true;
        }
        else {
            return false;
        }
    }
    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        // TODO: Remove this query filter when multitenancy is implemented
        var filterDate = DateTime.SpecifyKind(new DateTime(2021, 1, 1), DateTimeKind.Utc);

        builder.Entity<Form>().HasQueryFilter(form =>
            IsInternalUser() || (form.CreatedAt >= filterDate));

        builder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
        PrefixTableNames(builder);
    }

    public override int SaveChanges()
    {
        ProcessEntities();
        return base.SaveChanges();
    }

    /// <inheritdoc/>
    public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        ProcessEntities();
        return await base.SaveChangesAsync(true, cancellationToken);
    }

    private void ProcessEntities()
    {
        var entries = ChangeTracker.Entries()
            .Where(e => e.State == EntityState.Added || e.State == EntityState.Modified);

        foreach (var entry in entries)
        {
            switch (entry.State)
            {
                case EntityState.Added:
                    // Generate an id if necessary
                    if (entry.CurrentValues.Properties.Any(p => p.Name == "Id") &&
                        entry.CurrentValues["Id"] is default(long))
                    {
                        entry.CurrentValues["Id"] = _idGenerator.CreateId();
                    }

                    // Set the CreatedAt value
                    if (entry.CurrentValues.Properties.Any(p => p.Name == "CreatedAt"))
                    {
                        entry.CurrentValues["CreatedAt"] = DateTime.UtcNow;
                    }

                    break;
                case EntityState.Modified:
                    // Set the ModifiedAt value
                    if (entry.CurrentValues.Properties.Any(p => p.Name == "ModifiedAt"))
                    {
                        entry.CurrentValues["ModifiedAt"] = DateTime.UtcNow;
                    }

                    break;
            }
        }
    }

    private void PrefixTableNames(ModelBuilder builder)
    {
        Guard.Against.Null(builder);
        var entityTypes = builder.Model.GetEntityTypes();
        if (entityTypes is null || !entityTypes.Any())
        {
            return;
        }

        foreach (var entity in entityTypes)
        {
            if (ShouldPrefixTable(entity))
            {
                builder.Entity(entity.Name).ToTable(TableNamePrefix.GetTableName(entity.Name));
            }
        }
    }

    private bool ShouldPrefixTable(IMutableEntityType? entityType)
    {
        Guard.Against.Null(entityType);

        if (entityType.IsOwned())
        {
            return false;
        }

        return true;
    }
}
