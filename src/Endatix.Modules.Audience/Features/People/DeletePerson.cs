using Endatix.Core.Entities;
using Endatix.Core.Infrastructure.Domain;
using Endatix.Core.Infrastructure.Messaging;
using Endatix.Core.Infrastructure.Result;
using Endatix.Modules.Audience.Domain;
using Endatix.Modules.Audience.Persistence;

namespace Endatix.Modules.Audience.Features.People;

/// <summary>
/// Removes a person from this form only. The tenant <see cref="Member"/> remains.
/// </summary>
public sealed record DeletePersonCommand(long TenantId, long FormId, long MembershipId)
    : ICommand<Result<string>>;

/// <summary>Removes the person through <see cref="PersonRemoval"/>, after the form gate.</summary>
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

        Result<Membership> removed = await new PersonRemoval(db, membershipLock).ApplyAsync(
            new(request.FormId, request.MembershipId), cancellationToken);
        return removed.IsSuccess
            ? Result.Success(removed.Value.Id.ToString())
            : removed.ToErrorResult<string>();
    }
}
