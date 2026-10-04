using Endatix.Infrastructure.Data.Abstractions;
using Endatix.Modules.Personalization.Domain;
using Microsoft.EntityFrameworkCore;

namespace Endatix.Modules.Personalization.Persistence;

/// <summary>
/// Personalization DbContext surface used by handlers.
/// </summary>
public interface IPersonalizationDbContext : ITenantDbContext
{
    DbSet<AudienceSettings> AudienceSettings { get; }
    DbSet<Property> Properties { get; }
    DbSet<Member> Members { get; }
    DbSet<Membership> Memberships { get; }
    DbSet<PropertyValue> PropertyValues { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
