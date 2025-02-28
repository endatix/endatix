using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Endatix.Infrastructure.Data;

namespace Endatix.Infrastructure.Identity;

/// <summary>
/// This class integrates the AspNetCore EntityFramework Identity DB context, which also defines the Endatix specific details such as configs options and schema options
/// </summary>
public class AppIdentityDbContext : IdentityDbContext<AppUser, AppRole, long>
{
    private readonly EfCoreValueGeneratorFactory _valueGeneratorFactory;

    public AppIdentityDbContext(DbContextOptions options, EfCoreValueGeneratorFactory valueGeneratorFactory) : base(options)
    {
        _valueGeneratorFactory = valueGeneratorFactory;
    }

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
    }
}
