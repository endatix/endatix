using Endatix.Core.Abstractions;
using Microsoft.EntityFrameworkCore;
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
        if (context?.ChangeTracker.Entries().Any(entry =>
            entry.State is EntityState.Modified or EntityState.Deleted &&
            (entry.Entity is IInsertOnly || typeof(IInsertOnly).IsAssignableFrom(entry.Metadata.FindOwnership()?.PrincipalEntityType.ClrType))) == true)
        {
            throw new InvalidOperationException("An insert-only record cannot be updated or deleted.");
        }
    }
}
