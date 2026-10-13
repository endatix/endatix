using System.Globalization;
using Endatix.Infrastructure.Data;
using Endatix.Infrastructure.Data.Locking;
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
    public Task CommitAsync(CancellationToken cancellationToken) => transaction.CommitAsync(cancellationToken);

    public async ValueTask DisposeAsync()
    {
        await transaction.DisposeAsync();
        await app.Database.UseTransactionAsync(null);
    }
}
