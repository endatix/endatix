using Microsoft.EntityFrameworkCore;
using Endatix.Core.Entities;
using Endatix.Core.Abstractions;
using Endatix.Framework;

namespace Endatix.Infrastructure.Data;

/// <summary>
/// Represents the application database context for persisting the Endatix Domain entities
/// </summary>
public class AppDbContext : DbContext
{
    private readonly IIdGenerator<long> _idGenerator;

    protected AppDbContext() { }
    public AppDbContext(DbContextOptions<AppDbContext> options, IIdGenerator<long> idGenerator) : base(options)
    {
        _idGenerator = idGenerator;
    }

    public DbSet<Form> Forms { get; set; }

    public DbSet<FormDefinition> FormDefinitions { get; set; }

    public DbSet<Submission> Submissions { get; set; }

    public override void AddRange(IEnumerable<object> entities)
    {
        base.AddRange(entities);
    }

    public override void AddRange(params object[] entities)
    {
        base.AddRange(entities);
    }

    protected override void OnModelCreating(ModelBuilder builder)
    {
        builder.Ignore<Token>();

        var endatixAssemblies = GetType().Assembly.GetEndatixPlatformAssemblies();
        foreach (var assembly in endatixAssemblies)
        {
            builder.ApplyConfigurationsFromAssembly(assembly);
        }

        PrefixTableNames(builder);

        base.OnModelCreating(builder);

        builder.Entity<Submission>().OwnsOne(s => s.Token);
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
        if (builder?.Model?.GetEntityTypes() == null)
        {
            return;
        }

        foreach (var entity in builder.Model.GetEntityTypes())
        {
            builder.Entity(entity.Name).ToTable(TableNamePrefix.GetTableName(entity.Name));
        }
    }
}
