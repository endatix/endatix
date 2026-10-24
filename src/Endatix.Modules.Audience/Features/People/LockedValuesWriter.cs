using Endatix.Core.Abstractions.Data;
using Endatix.Core.Infrastructure.Result;
using Endatix.Modules.Audience.Domain;
using Endatix.Modules.Audience.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace Endatix.Modules.Audience.Features.People;

/// <summary>
/// Writes one participant's values under their <see cref="MembershipLock"/>, which their removal takes too: the
/// membership is read again under the lock, so values are written only while the participant is on the form, and a
/// removal that waited for the edit soft-deletes what it wrote. Every read and write runs on this writer's context, the
/// one that holds the lock.
/// </summary>
internal sealed class LockedValuesWriter(
    IAudienceDbContext db,
    MembershipLock membershipLock,
    IUniqueConstraintViolationChecker violations)
{
    /// <summary>
    /// The failure, or <see langword="null"/> once the values are written: 404 when the participant was removed before
    /// the lock was taken, 503 (<see cref="MembershipLock.Busy"/>) when the lock stayed held. A CSV import of the form
    /// can add the same cell first; it is committed by the time the index refuses this one, so a second locked pass
    /// finds it and overwrites it, in a new transaction.
    /// </summary>
    public async Task<IResult?> WriteAsync(ValuesEdit edit, CancellationToken cancellationToken)
    {
        try
        {
            return await WriteLockedAsync(edit, cancellationToken);
        }
        catch (DbUpdateException ex) when (violations.IsViolationOf(
            ex,
            PropertyValue.UniqueConstraints.CellPerMembership))
        {
            ((DbContext)db).ChangeTracker.Clear();
            return await WriteLockedAsync(edit, cancellationToken);
        }
    }

    private async Task<IResult?> WriteLockedAsync(ValuesEdit edit, CancellationToken cancellationToken)
    {
        Result<IDbContextTransaction> locked =
            await membershipLock.BeginEditAsync(db, edit.MembershipId, cancellationToken);
        return locked.IsSuccess ? await WriteInAsync(locked.Value, edit, cancellationToken) : locked;
    }

    private async Task<IResult?> WriteInAsync(
        IDbContextTransaction locked,
        ValuesEdit edit,
        CancellationToken cancellationToken)
    {
        await using IDbContextTransaction transaction = locked;
        if (await OnFormAsync(edit, cancellationToken) is not { } membership)
        {
            return Result.NotFound("Audience membership not found.");
        }

        await PropertyValuesWriter.UpsertAsync(new PropertyValueWrite(db, membership, edit.Values), cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return null;
    }

    private Task<Membership?> OnFormAsync(ValuesEdit edit, CancellationToken cancellationToken) =>
        db.Memberships.AsNoTracking().FirstOrDefaultAsync(
            membership => membership.TenantId == edit.TenantId
                && membership.FormId == edit.FormId
                && membership.Id == edit.MembershipId,
            cancellationToken);
}

/// <summary>The values to write on one participant of a form: property id to value, an empty value clearing its cell.</summary>
internal sealed record ValuesEdit(
    long TenantId,
    long FormId,
    long MembershipId,
    IReadOnlyDictionary<long, string> Values);
