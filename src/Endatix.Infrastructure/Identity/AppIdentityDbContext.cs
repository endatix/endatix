using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Endatix.Infrastructure.Data;
using Endatix.Core.Entities.Identity;
using Endatix.Core.Abstractions;

namespace Endatix.Infrastructure.Identity;

/// <summary>
/// This class integrates the AspNetCore EntityFramework Identity DB context, which also defines the Endatix specific details such as configs options and schema options
/// </summary>
public class AppIdentityDbContext : IdentityDbContext<AppUser, AppRole, long>
{
    private readonly EfCoreValueGeneratorFactory _valueGeneratorFactory;
    private readonly IIdGenerator<long> _idGenerator;

    public AppIdentityDbContext(DbContextOptions<AppIdentityDbContext> options, EfCoreValueGeneratorFactory valueGeneratorFactory, IIdGenerator<long> idGenerator) : base(options)
    {
        _valueGeneratorFactory = valueGeneratorFactory;
        _idGenerator = idGenerator;
    }

    public DbSet<EmailVerificationToken> EmailVerificationTokens { get; set; }

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.HasDefaultSchema("identity");

        builder.Entity<AppUser>()
               .Property(e => e.Id)
               .HasValueGenerator((property, _) => _valueGeneratorFactory.Create<long>(property))
               .ValueGeneratedNever();

        builder.Entity<AppRole>()
               .Property(e => e.Id)
                .HasValueGenerator((property, _) => _valueGeneratorFactory.Create<long>(property))
               .ValueGeneratedNever();

        builder.Entity<EmailVerificationToken>(entity =>
        {
            entity.ToTable("EmailVerificationTokens");

            entity.Property(t => t.Id)
                .IsRequired();

            entity.Property(t => t.UserId)
                .IsRequired();

            entity.Property(t => t.Token)
                .IsRequired()
                .HasMaxLength(64);

            entity.Property(t => t.ExpiresAt)
                .IsRequired();

            entity.Property(t => t.IsUsed)
                .IsRequired()
                .HasDefaultValue(false);

            // Create unique index on token value
            entity.HasIndex(t => t.Token)
                .IsUnique();

            // Create index on user ID for quick lookups
            entity.HasIndex(t => t.UserId);

            // Create index on expiry for cleanup operations
            entity.HasIndex(t => t.ExpiresAt);

            // Add foreign key relationship to AppUser
            entity.HasOne<AppUser>()
                .WithMany()
                .HasForeignKey(t => t.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });
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
}
