using FluentValidation;

namespace Endatix.Api.Common.Security;

/// <summary>
/// Reusable password validation rules that can be included in any validator
/// that needs to validate password complexity requirements.
/// </summary>
/// <remarks>
/// To use in your validators add:
/// Include(new PasswordValidator(propertyName));
/// </remarks>
public class PasswordValidator : AbstractValidator<string>
{
    /// <summary>
    /// Initializes centralized password validation rules for the specified property
    /// </summary>
    /// <param name="propertyName">Name of the password property being validated</param>
    public PasswordValidator(string propertyName = "Password") 
    {
        RuleFor(password => password)
            .NotEmpty()
            .WithMessage($"{propertyName} is required")
            .MinimumLength(8)
            .WithMessage($"{propertyName} must be at least 8 characters long")
            .Matches("[A-Z]")
            .WithMessage($"{propertyName} must contain at least one uppercase letter")
            .Matches("[a-z]") 
            .WithMessage($"{propertyName} must contain at least one lowercase letter")
            .Matches("[0-9]")
            .WithMessage($"{propertyName} must contain at least one number")
            .Matches("[^a-zA-Z0-9]")
            .WithMessage($"{propertyName} must contain at least one special character");
    }
}
