using System.Globalization;
using Endatix.Infrastructure.Data;
using Endatix.Infrastructure.Data.Locking;
using Endatix.Modules.Audience.Domain;
using Endatix.Modules.Audience.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace Endatix.Modules.Audience.Features.Links;

/// <summary>
/// Serializes the opens of one link that must not interleave: the first open, and the first token of a link
/// claimed before tokens were issued under this lock. Opens of different links never wait for each other.
/// </summary>
/// <remarks>
/// <see cref="AppDbContext"/> joins the audience connection and transaction for the locked section. A section
/// therefore holds one pooled connection, never one while it rents another, and its submission, token and claim
/// commit together.
/// </remarks>
internal sealed class LinkOpenLock(IAudienceDbContext db, AppDbContext app, ITransactionLock transactionLock)
{
    /// <summary>Long enough for a first open to finish; a waiter past it gets a retryable 503, not a held connection.</summary>
    internal static readonly TimeSpan Wait = TimeSpan.FromSeconds(5);

    /// <summary>
    /// The link's lock, then its form's shared <see cref="FormSubmissionsLock"/>, for a first open, which may add a
    /// submission: the form's deletion then waits for it, and an open during the deletion is refused at once. An open
    /// that takes the form's lock after a deletion finds the form gone when it reads it for the token. The two are
    /// always taken in this order, before anything is read or written, and no section takes a form's lock before
    /// another one, so the opens and the deletion never wait in a cycle.
    /// </summary>
    /// <exception cref="TransactionLockTimeoutException">Another open held the link for longer than <see cref="Wait"/>, or the form's deletion held the form.</exception>
    public async Task<LockedLink> BeginOpenAsync(AudienceLink link, CancellationToken cancellationToken)
    {
        var locked = await BeginAsync(link.Id, cancellationToken);
        try
        {
            await transactionLock.AcquireAsync(
                ((DbContext)db).Database,
                FormSubmissionsLock.Adding(link.FormId),
                cancellationToken);
            return locked;
        }
        catch
        {
            await locked.DisposeAsync();
            throw;
        }
    }

    /// <exception cref="TransactionLockTimeoutException">Another open held the link for longer than <see cref="Wait"/>.</exception>
    public async Task<LockedLink> BeginAsync(long linkId, CancellationToken cancellationToken)
    {
        var audience = ((DbContext)db).Database;
        var transaction = await transactionLock.BeginLockedTransactionAsync(
            audience,
            Request(linkId),
            cancellationToken);
        try
        {
            app.Database.SetDbConnection(audience.GetDbConnection());
            await app.Database.UseTransactionAsync(transaction.GetDbTransaction(), cancellationToken);
            return new LockedLink(app, transaction);
        }
        catch
        {
            await transaction.DisposeAsync();
            throw;
        }
    }

    private static TransactionLockRequest Request(long linkId) =>
        new(TransactionLockScopes.AudienceLinkOpen, linkId.ToString(CultureInfo.InvariantCulture))
        {
            Timeout = Wait,
        };
}

/// <summary>A locked section. Disposing it without <see cref="CommitAsync"/> rolls back everything it wrote.</summary>
internal sealed class LockedLink(AppDbContext app, IDbContextTransaction transaction) : IAsyncDisposable
{
    private bool _committed;

    public async Task CommitAsync(CancellationToken cancellationToken)
    {
        await transaction.CommitAsync(cancellationToken);
        _committed = true;
    }

    public async ValueTask DisposeAsync()
    {
        await transaction.DisposeAsync();
        await app.Database.UseTransactionAsync(null);
        if (!_committed)
        {
            // The rows rolled back, but the context still tracks them. An insert the unique index refused stays
            // Added, and the next save in this request would send it again.
            app.ChangeTracker.Clear();
        }
    }
}
