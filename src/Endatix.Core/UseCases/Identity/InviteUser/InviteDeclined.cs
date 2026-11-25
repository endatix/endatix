using Endatix.Core.Entities.Identity;
using Endatix.Core.Infrastructure.Result;

namespace Endatix.Core.UseCases.Identity.InviteUser;

/// <summary>An invite-specific conflict, distinct from conflicts returned by registration.</summary>
public sealed class InviteDeclined : Result<User>
{
    internal InviteDeclined() : base(ResultStatus.Conflict)
    {
        Errors = ["This workspace is not eligible to invite teammates yet."];
    }
}
