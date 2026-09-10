using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;

namespace Endatix.Infrastructure.Data;

/// <summary>
/// Save-time conventions shared by every Endatix <see cref="DbContext"/>.
/// </summary>
/// <remarks>
/// Extension methods rather than a base class on purpose. Contexts differ in what else they do at
/// save time — capturing integration events, generating ids from a value generator instead of here —
/// so each one composes the conventions it needs and the call site says which those are. A shared
/// base would force every context into one shape and change all of them silently when it moved.
/// </remarks>
public static class ChangeTrackerExtensions
{
    private const string CreatedAtPropertyName = "CreatedAt";
    private const string ModifiedAtPropertyName = "ModifiedAt";

    /// <summary>
    /// Applies <c>CreatedAt</c> on insert and <c>ModifiedAt</c> on update. Ids are assigned at
    /// aggregate <c>Create</c> or by <see cref="DbContextModelBuilderExtensions.ApplySnowflakeIdValueGenerators"/> on <c>Add</c>.
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
