using FastEndpoints;
using FluentValidation;

namespace Endatix.Api.Endpoints.Users;

/// <summary>
/// Validator for the GetUserRolesRequest.
/// </summary>
public class GetUserRolesValidator : Validator<GetUserRolesRequest>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="GetUserRolesValidator"/> class.
    /// </summary>
    public GetUserRolesValidator()
    {
        RuleFor(x => x.UserId)
            .GreaterThan(0);
    }
}
