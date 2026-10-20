using Endatix.Core.Abstractions.Forms;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace Endatix.Infrastructure.Data.Locking;

/// <summary>
/// A transaction that holds an <see cref="ITransactionLock"/>, and the context that writes in it: the one that began
/// it, or one joined to it. Disposing the section without <see cref="CommitAsync"/> rolls back everything written in
/// it.
/// </summary>
public sealed class LockedSection(IDbContextTransaction transaction, DbContext writer) : IFormSubmissionsSection
{
    private bool _committed;

    /// <inheritdoc />
    public async Task CommitAsync(CancellationToken cancellationToken)
    {
        await transaction.CommitAsync(cancellationToken);
        _committed = true;
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        await transaction.DisposeAsync();
        if (writer.Database.CurrentTransaction is not null)
        {
            // A context joined to another's transaction keeps using it until it is let go.
            await writer.Database.UseTransactionAsync(null);
        }

        if (!_committed)
        {
            // The rows rolled back, but the context still tracks them. An insert the unique index refused stays
            // Added, and the next save in this request would send it again.
            writer.ChangeTracker.Clear();
        }
    }
}
