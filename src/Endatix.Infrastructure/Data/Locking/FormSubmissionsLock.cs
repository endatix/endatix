using System.Globalization;
using Endatix.Core.Abstractions.Forms;
using Endatix.Core.Infrastructure.Result;

namespace Endatix.Infrastructure.Data.Locking;

/// <summary>
/// <see cref="IFormSubmissionsLock"/> as a <see cref="TransactionLockScopes.FormSubmissions"/> lock taken on
/// <see cref="AppDbContext"/>: shared by the adds, exclusive for the deletion.
/// </summary>
/// <remarks>
/// <para>
/// The key is the form id alone. Ids are unique across tenants, and a deletion takes the lock before it reads the
/// form again, so it does not need a tenant. Two forms must not share the lock: with an add's near-zero wait, a
/// shared lock would turn one form's deletion into refused creates on another.
/// </para>
/// <para>
/// PostgreSQL keys it by the request's 64-bit <see cref="TransactionLockRequest.Id"/>, so two forms never share it.
/// SQL Server names the resource <c>1119:{formId}</c>, at most 24 characters. It keeps the first 32 characters of a
/// resource name as they are and hashes only the rest, so two forms' names stay apart too; a key that grew past 32
/// characters would lose that.
/// </para>
/// </remarks>
public sealed class FormSubmissionsLock(AppDbContext db, ITransactionLock transactionLock) : IFormSubmissionsLock
{
    /// <summary>
    /// How long a deletion waits for the adds under way, or for another deletion of the form. Far longer than an add
    /// holds the lock; a deletion that waits for another one would find the form gone, so a retryable 503 serves it as
    /// well as a longer wait, without holding a pooled connection for it.
    /// </summary>
    internal static readonly TimeSpan DeletionWait = TimeSpan.FromSeconds(5);

    /// <summary>
    /// How long an add waits for a deletion: as short as both providers allow (a whole millisecond), so it answers
    /// 503 at once instead of queueing behind a deletion of many submissions with a pooled connection held. The add
    /// would find the form gone anyway.
    /// </summary>
    internal static readonly TimeSpan AddWait = TimeSpan.FromMilliseconds(1);

    /// <summary>The cause is left out: an add, or another deletion, may be what holds the form.</summary>
    private const string Busy = "The form is busy; try again shortly.";

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

    /// <summary>The lock a deletion takes.</summary>
    internal static TransactionLockRequest Deleting(long formId) =>
        Request(formId) with { Timeout = DeletionWait };

    private static TransactionLockRequest Request(long formId) =>
        new(TransactionLockScopes.FormSubmissions, formId.ToString(CultureInfo.InvariantCulture)) { Id = formId };

    private async Task<Result<IFormSubmissionsSection>> BeginAsync(
        TransactionLockRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var transaction = await transactionLock.BeginLockedTransactionAsync(db.Database, request, cancellationToken);
            return Result<IFormSubmissionsSection>.Success(new LockedSection(transaction, db));
        }
        catch (TransactionLockTimeoutException)
        {
            return Result<IFormSubmissionsSection>.Unavailable(Busy);
        }
    }
}
