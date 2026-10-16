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
    /// How long a deletion waits for the adds under way. Far longer than an add holds the lock; past it the deletion
    /// gets a retryable 503, not a connection held for the whole command timeout.
    /// </summary>
    internal static readonly TimeSpan DeletionWait = TimeSpan.FromSeconds(5);

    /// <summary>
    /// How long an add waits for a deletion: as short as both providers allow (a whole millisecond), so it answers
    /// 503 at once instead of queueing behind a deletion of many submissions with a pooled connection held. The add
    /// would find the form gone anyway.
    /// </summary>
    internal static readonly TimeSpan AddWait = TimeSpan.FromMilliseconds(1);

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
        Request(formId) with { Mode = TransactionLockMode.Shared, Timeout = AddWait };

    private static TransactionLockRequest Deleting(long formId) =>
        Request(formId) with { Timeout = DeletionWait };

    private static TransactionLockRequest Request(long formId) =>
        new(TransactionLockScopes.FormSubmissions, formId.ToString(CultureInfo.InvariantCulture));

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
