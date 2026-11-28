using Endatix.Core.Abstractions;
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
    private const string CREATED_AT_PROPERTY_NAME = "CreatedAt";
    private const string MODIFIED_AT_PROPERTY_NAME = "ModifiedAt";

    /// <summary>
    /// <c>CreatedAt</c> on insert, <c>ModifiedAt</c> on update. Ids come from OnAdd snowflake.
    /// </summary>
    public static void ApplyEndatixEntityDefaults(
        this ChangeTracker changeTracker,
        DateTime utcNow) =>
        changeTracker.ApplyEndatixEntityDefaults(utcNow, inspect: null);

    /// <summary>
    /// <c>CreatedAt</c> on insert, <c>ModifiedAt</c> on update, and <paramref name="inspect"/> called once for every
    /// tracked entry in the same scan, so a context can examine its save without a second pass over the change tracker.
    /// </summary>
    public static void ApplyEndatixEntityDefaults(
        this ChangeTracker changeTracker,
        DateTime utcNow,
        Action<EntityEntry>? inspect)
    {
        foreach (var entry in changeTracker.Entries())
        {
            inspect?.Invoke(entry);
            var properties = entry.CurrentValues.Properties;

            switch (entry.State)
            {
                case EntityState.Added:
                    if (properties.Any(property => property.Name == CREATED_AT_PROPERTY_NAME) &&
                        entry.CurrentValues[CREATED_AT_PROPERTY_NAME] is DateTime createdAt &&
                        createdAt == default)
                    {
                        entry.CurrentValues[CREATED_AT_PROPERTY_NAME] = utcNow;
                    }

                    break;

                case EntityState.Modified when entry.Entity is not IHasDomainModificationTime:
                    if (properties.Any(property => property.Name == MODIFIED_AT_PROPERTY_NAME))
                    {
                        entry.CurrentValues[MODIFIED_AT_PROPERTY_NAME] = utcNow;
                    }

                    break;
            }
        }
    }
}
