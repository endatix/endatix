using System.Diagnostics;
using System.Globalization;
using Endatix.Core.Infrastructure.Result;
using Endatix.Infrastructure.Data.Locking;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;

namespace Endatix.Modules.Audience.Persistence;

/// <summary>
/// Keeps the writers of one participant's values apart from their removal. An edit and a removal take their stripe of
/// the form's people lock (<see cref="TransactionLockScopes.AudienceFormPeople"/>) exclusively, which a CSV import of
/// the form holds shared, then the participant's lock (scope <see cref="TransactionLockScopes.AudienceMembership"/>,
/// key the membership id). Each section then reads the membership again, so no writer adds a live value to a
/// participant removed while it waited, and a removal that waited soft-deletes what the writer before it stored. An
/// edit never waits on an import's uncommitted value, which only the command timeout would bound: it waits for its
/// stripe instead, within <see cref="Wait"/>, before it holds anything.
/// </summary>
/// <remarks>
/// <para>
/// The form's people lock is <see cref="FormStripes"/> locks, keyed <c>{formId}:{stripe}</c>. A removal holds its
/// participant's stripe exclusively, and so does an edit; an import holds every stripe shared. So imports of one
/// form run together, edits and removals on different stripes run together, and an import and an edit or a removal
/// on the form always exclude each other. One shared/exclusive lock can't say that: imports would exclude each other,
/// or removals would. Two edits or removals on one stripe wait for each other for the length of one.
/// </para>
/// <para>
/// Order: the form's stripes before the participant's lock, an import's stripes in ascending order after its
/// <see cref="MatchKeyLock"/>; no section reverses either, so they never wait in a cycle. On PostgreSQL two keys of
/// one scope can share a <c>hashtext</c> lock, so unrelated participants or forms then wait for each other, within
/// <see cref="Wait"/>.
/// </para>
/// </remarks>
internal sealed class MembershipLock(ITransactionLock transactionLock)
{
    /// <summary>What an edit or a removal answers when another writer of the participant held their lock too long.</summary>
    internal const string Busy = "This participant is being changed. Try again.";

    /// <summary>
    /// What an edit, a removal or an import answers when the form's people lock stayed held: for an edit or a removal,
    /// by an import of the form or another edit or removal on its stripe; for an import, by an edit or a removal on the
    /// form.
    /// </summary>
    internal const string FormBusy = "People on this form are being imported or changed. Try again.";

    /// <summary>How many locks a form's people lock is split into: one is an edit's or a removal's, all an import's.</summary>
    internal const int FormStripes = 1 << StripeBits;

    private const int StripeBits = 4;

    // 2^64 / golden ratio: multiplying by it spreads every bit of an id into the top bits (Fibonacci hashing).
    private const ulong StripeMixer = 0x9E3779B97F4A7C15UL;

    /// <summary>Longer than an edit or a removal takes; a waiter past it gets a retryable 503, not a held connection.</summary>
    internal static readonly TimeSpan DefaultWait = TimeSpan.FromSeconds(5);

    // A lock timeout must be positive; a stripe whose share of Wait is spent still tries once.
    private static readonly TimeSpan ShortestWait = TimeSpan.FromMilliseconds(1);

    /// <summary>
    /// How long a section waits for each lock, and an import for all the form's stripes together; tests shorten or
    /// lengthen it.
    /// </summary>
    internal TimeSpan Wait { get; init; } = DefaultWait;

    /// <summary>
    /// The locks of <see cref="BeginRemovalAsync"/>, for an edit of the participant's values: an edit during an import
    /// of the form waits for its stripe, not for the import's uncommitted value.
    /// </summary>
    public Task<Result<IDbContextTransaction>> BeginEditAsync(
        IAudienceDbContext db,
        ParticipantLockKey participant,
        CancellationToken cancellationToken) =>
        BeginRemovalAsync(db, participant, cancellationToken);

    /// <summary>
    /// A transaction holding the participant's stripe of the form's people lock and then the participant's lock, for
    /// their removal; 503 when an import of the form, another removal on the stripe or another writer of the
    /// participant holds it, with the message of the lock that waited.
    /// </summary>
    public Task<Result<IDbContextTransaction>> BeginRemovalAsync(
        IAudienceDbContext db,
        ParticipantLockKey participant,
        CancellationToken cancellationToken) =>
        UnlessBusyAsync(() => BeginBothAsync(Database(db), participant, cancellationToken));

    /// <summary>
    /// Every stripe of the form's people lock shared, in the import's transaction, after its
    /// <see cref="MatchKeyLock"/>: the failure (503 while a removal on the form holds a stripe past
    /// <see cref="Wait"/>), or <see langword="null"/> once all are held. Other imports of the form hold them too.
    /// </summary>
    public async Task<IResult?> AcquireImportAsync(
        IAudienceDbContext db,
        long formId,
        CancellationToken cancellationToken)
    {
        Result<bool> held = await UnlessBusyAsync(async () =>
        {
            await AcquireEveryStripeAsync(Database(db), formId, cancellationToken);
            return true;
        });
        return held.IsSuccess ? null : held;
    }

    /// <summary>The lock's result, or 503 with the busy message of the lock whose wait ran out.</summary>
    private static async Task<Result<T>> UnlessBusyAsync<T>(Func<Task<T>> take)
    {
        try
        {
            return Result<T>.Success(await take());
        }
        catch (TransactionLockTimeoutException timeout)
        {
            return Result<T>.Unavailable(
                timeout.Request.Scope == TransactionLockScopes.AudienceFormPeople ? FormBusy : Busy);
        }
    }

    /// <summary>
    /// The participant's stripe of their form's people lock, from the top bits of their mixed membership id. Not the id
    /// modulo <see cref="FormStripes"/>: an id's low bits are its snowflake's per-millisecond sequence, 0 or 1 for a
    /// participant added on their own, so nearly every such participant would share one stripe.
    /// </summary>
    internal static int FormStripe(long membershipId) =>
        (int)(unchecked((ulong)membershipId * StripeMixer) >> (64 - StripeBits));

    // In one order, so imports never wait in a cycle; each stripe waits for what is left of one Wait.
    private async Task AcquireEveryStripeAsync(
        DatabaseFacade database,
        long formId,
        CancellationToken cancellationToken)
    {
        var waited = Stopwatch.StartNew();
        for (var stripe = 0; stripe < FormStripes; stripe++)
        {
            var left = Wait - waited.Elapsed;
            await transactionLock.AcquireAsync(
                database,
                FormPeople(formId, stripe) with
                {
                    Mode = TransactionLockMode.Shared,
                    Timeout = left > ShortestWait ? left : ShortestWait,
                },
                cancellationToken);
        }
    }

    private async Task<IDbContextTransaction> BeginBothAsync(
        DatabaseFacade database,
        ParticipantLockKey keys,
        CancellationToken cancellationToken)
    {
        IDbContextTransaction transaction = await transactionLock.BeginLockedTransactionAsync(
            database,
            FormPeople(keys.FormId, FormStripe(keys.MembershipId)),
            cancellationToken);
        try
        {
            await transactionLock.AcquireAsync(database, Participant(keys.MembershipId), cancellationToken);
            return transaction;
        }
        catch
        {
            await transaction.DisposeAsync();
            throw;
        }
    }

    private TransactionLockRequest Participant(long membershipId) =>
        new(TransactionLockScopes.AudienceMembership, membershipId.ToString(CultureInfo.InvariantCulture))
        {
            Timeout = Wait,
        };

    private TransactionLockRequest FormPeople(long formId, int stripe) =>
        new(TransactionLockScopes.AudienceFormPeople, string.Create(CultureInfo.InvariantCulture, $"{formId}:{stripe}"))
        {
            Timeout = Wait,
        };

    private static DatabaseFacade Database(IAudienceDbContext db) => ((DbContext)db).Database;
}

/// <summary>A participant by their form and membership, the keys of their edit's and their removal's locks.</summary>
internal sealed record ParticipantLockKey(long FormId, long MembershipId);
