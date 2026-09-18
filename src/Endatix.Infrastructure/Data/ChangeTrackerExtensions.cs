using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;

namespace Endatix.Infrastructure.Data;

/// <summary>
/// Save-time conventions shared by every Endatix <see cref="DbContext"/>.
/// Extension methods rather than a base class: contexts differ in save-time work
/// (outbox capture vs timestamps only).
/// </summary>
public static class ChangeTrackerExtensions
{
    private const string CreatedAtPropertyName = "CreatedAt";
    private const string ModifiedAtPropertyName = "ModifiedAt";

    /// <summary>
    /// <c>CreatedAt</c> on insert, <c>ModifiedAt</c> on update. Ids come from OnAdd snowflake.
    /// </summary>
    public static void ApplyEndatixEntityDefaults(
        this ChangeTracker changeTracker,
        DateTime utcNow)
    {
        var entries = changeTracker.Entries()
            .Where(entry => entry.State is EntityState.Added or EntityState.Modified);

        foreach (var entry in entries)
        {
            var properties = entry.CurrentValues.Properties;

            switch (entry.State)
            {
                case EntityState.Added:
                    if (properties.Any(property => property.Name == CreatedAtPropertyName) &&
                        entry.CurrentValues[CreatedAtPropertyName] is DateTime createdAt &&
                        createdAt == default)
                    {
                        entry.CurrentValues[CreatedAtPropertyName] = utcNow;
                    }

                    break;

                case EntityState.Modified:
                    if (properties.Any(property => property.Name == ModifiedAtPropertyName))
                    {
                        entry.CurrentValues[ModifiedAtPropertyName] = utcNow;
                    }

                    break;
            }
        }
    }
}
