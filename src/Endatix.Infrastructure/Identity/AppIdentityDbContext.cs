using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Endatix.Core.Abstractions;
using Endantix.Infrastructure.Data;

namespace Endatix.Infrastructure.Identity;

/// <summary>
/// This class integrates the AspNetCore EntityFramework Identity DB context, which also defines the Endatix specific details such as configs options and schema options
/// </summary>
public class AppIdentityDbContext : IdentityDbContext<AppUser, AppRole, long>
{
    private readonly IIdGenerator<long> _idGenerator;

    public AppIdentityDbContext(DbContextOptions<AppIdentityDbContext> options, IIdGenerator<long> idGenerator) : base(options)
    {
        _idGenerator = idGenerator;
    }

    protected override void OnModelCreating(ModelBuilder builder)
    {
        builder.HasDefaultSchema("identity");

        base.OnModelCreating(builder);

        builder.Entity<AppUser>()
               .Property(e => e.Id)
               .HasValueGenerator((_, _) => new SnowflakeValueGenerator(_idGenerator))
               .ValueGeneratedNever();

        builder.Entity<AppRole>()
               .Property(e => e.Id)
               .HasValueGenerator((_, _) => new SnowflakeValueGenerator(_idGenerator))
               .ValueGeneratedNever();
    }
}
