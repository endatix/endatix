using Endatix.Core.Abstractions.Data;
using Endatix.Modules.Audience.Domain;
using Endatix.Modules.Audience.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace Endatix.Modules.Audience.Features.People;

/// <summary>
/// Writes one participant's values under their <see cref="MembershipLock"/>, which their removal takes too: the
/// membership is read again under the lock, so values are written only while the participant is on the form, and a
/// removal that waited for the edit soft-deletes what it wrote.
/// </summary>
internal sealed class LockedValuesWriter(
    IAudienceDbContext db,
    MembershipLock membershipLock,
    IUniqueConstraintViolationChecker violations)
{
    /// <summary>
    /// <see langword="false"/> when the participant was removed before the lock was taken. A writer that skips the
    /// lock (the CSV import) can add the same cell first; it is committed by the time the index refuses this one, so a
    /// second locked pass finds it and overwrites it, in a new transaction.
    /// </summary>
    /// <exception cref="Endatix.Infrastructure.Data.Locking.TransactionLockTimeoutException">The removal or another edit held the participant past <see cref="MembershipLock.Wait"/>.</exception>
    public async Task<bool> WriteAsync(PropertyValueWrite write, CancellationToken cancellationToken)
    {
        try
        {
            return await WriteLockedAsync(write, cancellationToken);
        }
        catch (DbUpdateException ex) when (violations.IsViolationOf(
            ex,
            PropertyValue.UniqueConstraints.CellPerMembership))
        {
            ((DbContext)db).ChangeTracker.Clear();
            return await WriteLockedAsync(write, cancellationToken);
        }
    }

    private async Task<bool> WriteLockedAsync(PropertyValueWrite write, CancellationToken cancellationToken)
    {
        await using IDbContextTransaction transaction =
            await membershipLock.BeginAsync(db, write.MembershipId, cancellationToken);
        bool onForm = await db.Memberships.AsNoTracking().AnyAsync(
            membership => membership.TenantId == write.TenantId && membership.Id == write.MembershipId,
            cancellationToken);
        if (!onForm)
        {
            return false;
        }

        await PropertyValuesWriter.UpsertAsync(write, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return true;
    }
}
