using FastEndpoints;
using FluentValidation;

namespace Endatix.Api.Endpoints.Users;

/// <summary>
/// Validator for the <see cref="UserIdRequest"/> class.
/// </summary>
public sealed class UserIdValidator : Validator<UserIdRequest>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="UserIdValidator"/> class.
    /// </summary>
    public UserIdValidator()
    {
        RuleFor(x => x.UserId).GreaterThan(0);
    }
}
