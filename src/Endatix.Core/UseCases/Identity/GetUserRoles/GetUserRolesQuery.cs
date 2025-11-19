using Ardalis.GuardClauses;
using Endatix.Core.Infrastructure.Messaging;
using Endatix.Core.Infrastructure.Result;

namespace Endatix.Core.UseCases.Identity.GetUserRoles;

/// <summary>
/// Query to retrieve roles assigned to a user.
/// </summary>
public record GetUserRolesQuery : IQuery<Result<IList<string>>>
{
    /// <summary>
    /// The ID of the user to get roles for.
    /// </summary>
    public long UserId { get; init; }

    public GetUserRolesQuery(long userId)
    {
        Guard.Against.NegativeOrZero(userId, nameof(userId));
        UserId = userId;
    }
}
