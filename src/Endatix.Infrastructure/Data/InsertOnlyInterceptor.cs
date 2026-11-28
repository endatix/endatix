using Endatix.Core.Abstractions;
using Endatix.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Endatix.Infrastructure.Data;

internal sealed class InsertOnlyInterceptor : SaveChangesInterceptor
{
    internal static InsertOnlyInterceptor Instance { get; } = new();

    public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
    {
        RefuseUpdates(eventData.Context);
        return result;
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData,
        InterceptionResult<int> result, CancellationToken cancellationToken = default)
    {
        RefuseUpdates(eventData.Context);
        return ValueTask.FromResult(result);
    }

    private static void RefuseUpdates(DbContext? context)
    {
        if (context?.ChangeTracker.Entries().Any(IsHistoryWrite) == true)
        {
            throw new InvalidOperationException("An insert-only record cannot be updated or deleted.");
        }
    }

    private static bool IsHistoryWrite(EntityEntry entry)
    {
        if (entry.State is not (EntityState.Modified or EntityState.Deleted))
        {
            return false;
        }
        if (entry.Entity is FormDefinition definition)
        {
            return definition.PublishedFromDraftVersion.HasValue ||
                entry.OriginalValues[nameof(FormDefinition.PublishedFromDraftVersion)] is not null;
        }
        return entry.Entity is IInsertOnly ||
            typeof(IInsertOnly).IsAssignableFrom(entry.Metadata.FindOwnership()?.PrincipalEntityType.ClrType);
    }
}
