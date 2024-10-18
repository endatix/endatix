using FastEndpoints;
using FluentValidation;

namespace Endatix.Api.Endpoints.Auth;

/// <summary>
/// Validation rules for the <c>RefreshTokenRequest</c> class.
/// </summary>
public class RefreshTokenValidator : Validator<RefreshTokenRequest>
{
    /// <summary>
    /// Default constructor
    /// </summary>
    public RefreshTokenValidator()
    {
        RuleFor(x => x.Authorization)
            .NotEmpty()
            .Must(auth => auth.StartsWith("Bearer ")).WithMessage("Authorization header must start with 'Bearer '.");

        RuleFor(x => x.RefreshToken)
            .NotEmpty();
    }
}
