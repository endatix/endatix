using Endatix.Core.Entities;
using Endatix.Core.Infrastructure.Domain;
using Endatix.Core.Infrastructure.Messaging;
using Endatix.Core.Infrastructure.Result;
using Endatix.Infrastructure.Data.Locking;
using Endatix.Modules.Audience.Domain;
using Endatix.Modules.Audience.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace Endatix.Modules.Audience.Features.People;

/// <summary>
/// Removes a person from this form only. The tenant <see cref="Member"/> remains.
/// </summary>
public sealed record DeletePersonCommand(long TenantId, long FormId, long MembershipId)
    : ICommand<Result<string>>;

internal sealed class DeletePersonHandler(
    IAudienceDbContext db,
    IRepository<Form> forms,
    MembershipLock membershipLock)
    : ICommandHandler<DeletePersonCommand, Result<string>>
{
    public async Task<Result<string>> Handle(
        DeletePersonCommand request,
        CancellationToken cancellationToken)
    {
        Result gate = await TenantFormGate.EnsureAsync(
            new FormGateRequest(forms, request.TenantId, request.FormId, cancellationToken));
        if (!gate.IsSuccess)
        {
            return gate.ToErrorResult<string>();
        }

        try
        {
            return await DeleteLockedAsync(request, cancellationToken);
        }
        catch (TransactionLockTimeoutException)
        {
            return Result.Unavailable(UpdatePersonHandler.Busy);
        }
    }

    /// <summary>
    /// Under the participant's <see cref="MembershipLock"/>, which an edit of their values takes too: the membership
    /// and its cells are read after the lock, so an edit that finished first has its cells deleted too, and one that
    /// waits finds the membership gone.
    /// </summary>
    private async Task<Result<string>> DeleteLockedAsync(
        DeletePersonCommand request,
        CancellationToken cancellationToken)
    {
        await using IDbContextTransaction transaction =
            await membershipLock.BeginAsync(db, request.MembershipId, cancellationToken);
        Result<Membership> loaded = await LoadAsync(request, cancellationToken);
        if (!loaded.IsSuccess)
        {
            return loaded.ToErrorResult<string>();
        }

        await SoftDeleteAsync(loaded.Value, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return Result.Success(loaded.Value.Id.ToString());
    }

    private async Task<Result<Membership>> LoadAsync(
        DeletePersonCommand request,
        CancellationToken cancellationToken)
    {
        Membership? membership = await db.Memberships.FirstOrDefaultAsync(
            row => row.Id == request.MembershipId && row.FormId == request.FormId,
            cancellationToken);
        return membership is null
            ? Result.NotFound("Audience membership not found.")
            : Result.Success(membership);
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
