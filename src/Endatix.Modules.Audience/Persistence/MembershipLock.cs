using System.Globalization;
using Endatix.Core.Infrastructure.Result;
using Endatix.Infrastructure.Data.Locking;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;

namespace Endatix.Modules.Audience.Persistence;

/// <summary>
/// Keeps the writers of one participant's values apart from their removal. An edit and a removal take the
/// participant's lock (scope <see cref="TransactionLockScopes.AudienceMembership"/>, key the membership id); a removal
/// first shares its form's lock (<see cref="TransactionLockScopes.AudienceFormPeople"/>, key the form id), which a CSV
/// import takes exclusively. Each section then reads the membership again, so no writer adds a live value to a
/// participant removed while it waited, and a removal that waited soft-deletes what the writer before it stored.
/// </summary>
/// <remarks>
/// Order: the form's lock before the participant's, and an import's <see cref="MatchKeyLock"/> before the form's; no
/// section reverses either, so they never wait in a cycle. On PostgreSQL two keys of one scope can share a
/// <c>hashtext</c> lock, so unrelated participants or forms then wait for each other, within <see cref="Wait"/>.
/// </remarks>
internal sealed class MembershipLock(ITransactionLock transactionLock)
{
    /// <summary>What an edit or a removal answers when the participant, or an import of their form, held it too long.</summary>
    internal const string Busy = "This participant is being changed. Try again.";

    /// <summary>What an import answers when a removal on its form held the form too long.</summary>
    internal const string FormBusy = "People on this form are being changed. Try again.";

    /// <summary>Longer than an edit or a removal takes; a waiter past it gets a retryable 503, not a held connection.</summary>
    internal static readonly TimeSpan DefaultWait = TimeSpan.FromSeconds(5);

    /// <summary>How long a section waits for each lock; tests shorten or lengthen it.</summary>
    internal TimeSpan Wait { get; init; } = DefaultWait;

    /// <summary>A transaction holding the participant's lock, for an edit of their values; 503 when it stays held.</summary>
    public async Task<Result<IDbContextTransaction>> BeginEditAsync(
        IAudienceDbContext db,
        long membershipId,
        CancellationToken cancellationToken)
    {
        try
        {
            return Result<IDbContextTransaction>.Success(
                await transactionLock.BeginLockedTransactionAsync(Database(db), Participant(membershipId), cancellationToken));
        }
        catch (TransactionLockTimeoutException)
        {
            return Result<IDbContextTransaction>.Unavailable(Busy);
        }
    }

    /// <summary>
    /// A transaction holding the form's lock shared and then the participant's, for their removal; 503 when an import
    /// of the form or another writer of the participant holds it.
    /// </summary>
    public async Task<Result<IDbContextTransaction>> BeginRemovalAsync(
        IAudienceDbContext db,
        RemovalKey participant,
        CancellationToken cancellationToken)
    {
        try
        {
            return Result<IDbContextTransaction>.Success(
                await BeginBothAsync(Database(db), participant, cancellationToken));
        }
        catch (TransactionLockTimeoutException)
        {
            return Result<IDbContextTransaction>.Unavailable(Busy);
        }
    }

    /// <summary>
    /// The form's lock exclusively, in the import's transaction, after its <see cref="MatchKeyLock"/>: the failure (503
    /// while a removal on the form holds it), or <see langword="null"/> once it is held.
    /// </summary>
    public async Task<IResult?> AcquireImportAsync(
        IAudienceDbContext db,
        long formId,
        CancellationToken cancellationToken)
    {
        try
        {
            await transactionLock.AcquireAsync(Database(db), FormPeople(formId), cancellationToken);
            return null;
        }
        catch (TransactionLockTimeoutException)
        {
            return Result.Unavailable(FormBusy);
        }
    }

    private async Task<IDbContextTransaction> BeginBothAsync(
        DatabaseFacade database,
        RemovalKey keys,
        CancellationToken cancellationToken)
    {
        IDbContextTransaction transaction = await transactionLock.BeginLockedTransactionAsync(
            database,
            FormPeople(keys.FormId) with { Mode = TransactionLockMode.Shared },
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

    private TransactionLockRequest FormPeople(long formId) =>
        new(TransactionLockScopes.AudienceFormPeople, formId.ToString(CultureInfo.InvariantCulture))
        {
            Timeout = Wait,
        };

    private static DatabaseFacade Database(IAudienceDbContext db) => ((DbContext)db).Database;
}

/// <summary>A participant by their form and membership, the two keys of their removal's locks.</summary>
internal sealed record RemovalKey(long FormId, long MembershipId);
