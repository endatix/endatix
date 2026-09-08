using Endatix.Core.Abstractions;
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
    private const string IdPropertyName = "Id";
    private const string CreatedAtPropertyName = "CreatedAt";
    private const string ModifiedAtPropertyName = "ModifiedAt";

    /// <summary>
    /// Applies the entity defaults Endatix expects on every write: an id for new entities,
    /// <c>CreatedAt</c> on insert, and <c>ModifiedAt</c> on update.
    /// </summary>
    /// <param name="changeTracker">The tracker whose pending entries are stamped.</param>
    /// <param name="utcNow">Timestamp applied to the affected entries.</param>
    /// <param name="idGenerator">
    /// Assigns ids to new entities whose key is still unset. Pass <see langword="null"/> when the
    /// context assigns keys another way — a context using an EF value generator already has its ids
    /// by the time an entity is tracked, and stamping again here would be a no-op at best.
    /// </param>
    public static void ApplyEndatixEntityDefaults(
        this ChangeTracker changeTracker,
        DateTime utcNow,
        IIdGenerator<long>? idGenerator = null)
    {
        var entries = changeTracker.Entries()
            .Where(entry => entry.State is EntityState.Added or EntityState.Modified);

        foreach (var entry in entries)
        {
            var properties = entry.CurrentValues.Properties;

            switch (entry.State)
            {
                case EntityState.Added:
                    if (idGenerator is not null &&
                        properties.Any(property => property.Name == IdPropertyName) &&
                        entry.CurrentValues[IdPropertyName] is long id && id == default)
                    {
                        entry.CurrentValues[IdPropertyName] = idGenerator.CreateId();
                    }

                    // Only when unset: a caller that supplied CreatedAt explicitly (seeding, an
                    // import preserving original timestamps) must not have it overwritten.
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
