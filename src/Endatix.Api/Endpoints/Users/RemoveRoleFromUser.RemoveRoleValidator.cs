using FastEndpoints;
using FluentValidation;

namespace Endatix.Api.Endpoints.Users;

/// <summary>
/// Validator for the RemoveRoleRequest.
/// </summary>
public class RemoveRoleValidator : Validator<RemoveRoleRequest>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="RemoveRoleValidator"/> class.
    /// </summary>
    public RemoveRoleValidator()
    {
        RuleFor(x => x.UserId)
            .GreaterThan(0);

        RuleFor(x => x.RoleName)
            .NotEmpty();
    }
}
