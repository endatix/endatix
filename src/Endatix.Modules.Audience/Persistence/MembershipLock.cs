using System.Globalization;
using Endatix.Infrastructure.Data.Locking;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace Endatix.Modules.Audience.Persistence;

/// <summary>
/// Keeps an edit of one participant's values apart from their removal. Both take it exclusively as their
/// transaction's first statement, then read the membership again, so an edit never writes a live value onto a
/// participant removed while it waited. Participants never wait for each other.
/// </summary>
internal sealed class MembershipLock(ITransactionLock transactionLock)
{
    /// <summary>Longer than an edit or a removal takes; a waiter past it gets a retryable 503, not a held connection.</summary>
    internal static readonly TimeSpan Wait = TimeSpan.FromSeconds(5);

    /// <exception cref="TransactionLockTimeoutException">Another edit or the removal held the participant past <see cref="Wait"/>.</exception>
    public Task<IDbContextTransaction> BeginAsync(
        IAudienceDbContext db,
        long membershipId,
        CancellationToken cancellationToken) =>
        transactionLock.BeginLockedTransactionAsync(
            ((DbContext)db).Database,
            new TransactionLockRequest(
                TransactionLockScopes.AudienceMembership,
                membershipId.ToString(CultureInfo.InvariantCulture))
            {
                Timeout = Wait,
            },
            cancellationToken);
}
