using FastEndpoints;
using FluentValidation;

namespace Endatix.Api.Endpoints.Roles;

/// <summary>
/// Validator for the CreateRoleRequest.
/// </summary>
public class CreateRoleValidator : Validator<CreateRoleRequest>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="CreateRoleValidator"/> class.
    /// </summary>
    public CreateRoleValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty()
            .MaximumLength(256);

        RuleFor(x => x.Description)
            .MaximumLength(500)
            .When(x => !string.IsNullOrEmpty(x.Description));

        RuleFor(x => x.Permissions)
            .NotEmpty();

        RuleForEach(x => x.Permissions)
            .NotEmpty();
    }
}
