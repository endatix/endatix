using Endatix.Infrastructure.Data.Abstractions;
using Endatix.Modules.Audience.Domain;
using Microsoft.EntityFrameworkCore;

namespace Endatix.Modules.Audience.Persistence;

/// <summary>
/// Audience DbContext surface used by handlers.
/// </summary>
public interface IAudienceDbContext : ITenantDbContext
{
    DbSet<AudienceSettings> AudienceSettings { get; }
    DbSet<Property> Properties { get; }
    DbSet<Member> Members { get; }
    DbSet<Membership> Memberships { get; }
    DbSet<PropertyValue> PropertyValues { get; }
    DbSet<AudienceImport> AudienceImports { get; }
    DbSet<AudienceLink> Links { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
