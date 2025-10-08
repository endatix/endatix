using FastEndpoints;
using FluentValidation;
using AuthRoles = Endatix.Infrastructure.Identity.Authorization.Roles;

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
            .NotEmpty()
            .Must(roleName => AuthRoles.IsValidRole(roleName))
            .WithMessage(x => $"Invalid role name '{x.RoleName}'. Valid roles are: {string.Join(", ", AuthRoles.AllRoles)}.");
    }
}
