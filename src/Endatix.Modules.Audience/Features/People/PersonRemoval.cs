using Endatix.Core.Infrastructure.Result;
using Endatix.Modules.Audience.Domain;
using Endatix.Modules.Audience.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace Endatix.Modules.Audience.Features.People;

/// <summary>
/// Removes one participant from their form: soft-deletes the membership and its cells, in a transaction of its own.
/// The tenant <see cref="Member"/> remains.
/// </summary>
/// <remarks>
/// Under the participant's stripe of the form's people lock and their <see cref="MembershipLock"/>, which an edit of
/// their values takes too and a CSV import of the form excludes: the membership and its cells are read after the
/// locks, so an edit or an import that finished first has its cells deleted too, and an edit that waits finds the
/// membership gone.
/// </remarks>
internal sealed class PersonRemoval(IAudienceDbContext db, MembershipLock membershipLock)
{
    /// <returns>
    /// The removed membership; <see cref="ResultStatus.NotFound"/> when it is not on the form or was removed already;
    /// <see cref="ResultStatus.Unavailable"/> when a lock stayed held.
    /// </returns>
    public async Task<Result<Membership>> ApplyAsync(ParticipantLockKey participant, CancellationToken cancellationToken)
    {
        Result<IDbContextTransaction> locked = await membershipLock.BeginRemovalAsync(db, participant, cancellationToken);
        return locked.IsSuccess
            ? await RemoveInAsync(locked.Value, participant, cancellationToken)
            : locked.ToErrorResult<Membership>();
    }

    private async Task<Result<Membership>> RemoveInAsync(
        IDbContextTransaction locked,
        ParticipantLockKey participant,
        CancellationToken cancellationToken)
    {
        await using IDbContextTransaction transaction = locked;
        Membership? membership = await db.Memberships.FirstOrDefaultAsync(
            row => row.Id == participant.MembershipId && row.FormId == participant.FormId,
            cancellationToken);
        if (membership is null)
        {
            return Result.NotFound("Audience membership not found.");
        }

        await SoftDeleteAsync(membership, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return Result.Success(membership);
    }

    private async Task SoftDeleteAsync(Membership membership, CancellationToken cancellationToken)
    {
        List<PropertyValue> values = await db.PropertyValues
            .Where(value => value.TenantId == membership.TenantId && value.MembershipId == membership.Id)
            .ToListAsync(cancellationToken);

        foreach (PropertyValue value in values)
        {
            value.Delete();
        }

        membership.Delete();
        await db.SaveChangesAsync(cancellationToken);
    }
}
