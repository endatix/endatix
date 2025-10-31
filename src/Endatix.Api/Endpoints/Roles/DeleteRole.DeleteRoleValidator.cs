using FastEndpoints;
using FluentValidation;

namespace Endatix.Api.Endpoints.Roles;

/// <summary>
/// Validator for the DeleteRoleRequest.
/// </summary>
public class DeleteRoleValidator : Validator<DeleteRoleRequest>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="DeleteRoleValidator"/> class.
    /// </summary>
    public DeleteRoleValidator()
    {
        RuleFor(x => x.RoleName)
            .NotEmpty()
            .MaximumLength(256);
    }
}
