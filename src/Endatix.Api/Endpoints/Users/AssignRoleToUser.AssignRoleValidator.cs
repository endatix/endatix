using FastEndpoints;
using FluentValidation;

namespace Endatix.Api.Endpoints.Users;

/// <summary>
/// Validator for the AssignRoleRequest.
/// </summary>
public class AssignRoleValidator : Validator<AssignRoleRequest>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="AssignRoleValidator"/> class.
    /// </summary>
    public AssignRoleValidator()
    {
        RuleFor(x => x.UserId)
            .GreaterThan(0);

        RuleFor(x => x.RoleName)
            .NotEmpty();
    }
}
