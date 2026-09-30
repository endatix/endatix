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
    DbSet<AudienceProperty> AudienceProperties { get; }
    DbSet<AudienceMember> AudienceMembers { get; }
    DbSet<AudienceMembership> AudienceMemberships { get; }
    DbSet<AudiencePropertyValue> AudiencePropertyValues { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
