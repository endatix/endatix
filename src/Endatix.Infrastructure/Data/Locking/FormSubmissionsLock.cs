using System.Globalization;
using Endatix.Core.Abstractions.Forms;
using Endatix.Core.Infrastructure.Result;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace Endatix.Infrastructure.Data.Locking;

/// <summary>
/// <see cref="IFormSubmissionsLock"/> as a <see cref="TransactionLockScopes.FormSubmissions"/> lock taken on
/// <see cref="AppDbContext"/>: shared by the adds, exclusive for the deletion.
/// </summary>
/// <remarks>
/// The key is the form id alone. Ids are unique across tenants, and a deletion takes the lock before it reads the
/// form, so it knows no tenant yet.
/// </remarks>
public sealed class FormSubmissionsLock(AppDbContext db, ITransactionLock transactionLock) : IFormSubmissionsLock
{
    /// <summary>
    /// Far longer than an add or a deletion holds the lock; a waiter past it gets a retryable 503, not a connection
    /// held for the whole command timeout.
    /// </summary>
    internal static readonly TimeSpan Wait = TimeSpan.FromSeconds(5);

    private const string Busy = "This form is busy. Try again in a moment.";

    /// <inheritdoc />
    public Task<Result<IFormSubmissionsSection>> BeginAddingAsync(long formId, CancellationToken cancellationToken) =>
        BeginAsync(Adding(formId), cancellationToken);

    /// <inheritdoc />
    public Task<Result<IFormSubmissionsSection>> BeginDeletingAsync(long formId, CancellationToken cancellationToken) =>
        BeginAsync(Deleting(formId), cancellationToken);

    /// <summary>
    /// The lock an add takes, for a section whose transaction already holds another lock (the first open of a
    /// personalised link). Such a section takes this one second, before it reads or writes anything.
    /// </summary>
    public static TransactionLockRequest Adding(long formId) =>
        Deleting(formId) with { Mode = TransactionLockMode.Shared };

    private static TransactionLockRequest Deleting(long formId) =>
        new(TransactionLockScopes.FormSubmissions, formId.ToString(CultureInfo.InvariantCulture))
        {
            Timeout = Wait,
        };

    private async Task<Result<IFormSubmissionsSection>> BeginAsync(
        TransactionLockRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var transaction = await transactionLock.BeginLockedTransactionAsync(db.Database, request, cancellationToken);
            return Result<IFormSubmissionsSection>.Success(new LockedFormSubmissions(db, transaction));
        }
        catch (TransactionLockTimeoutException)
        {
            return Result<IFormSubmissionsSection>.Unavailable(Busy);
        }
    }

    private sealed class LockedFormSubmissions(DbContext db, IDbContextTransaction transaction) : IFormSubmissionsSection
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
            if (!_committed)
            {
                // The rows rolled back, but the context still tracks them. An insert the unique index refused stays
                // Added, and the next save in this request would send it again.
                db.ChangeTracker.Clear();
            }
        }
    }
}
