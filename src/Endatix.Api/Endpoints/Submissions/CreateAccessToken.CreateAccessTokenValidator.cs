using FastEndpoints;
using FluentValidation;
using Endatix.Core.Abstractions.Submissions;

namespace Endatix.Api.Endpoints.Submissions;

/// <summary>
/// Validation rules for the <c>CreateAccessTokenRequest</c> class.
/// </summary>
public class CreateAccessTokenValidator : Validator<CreateAccessTokenRequest>
{
    /// <summary>
    /// Default constructor
    /// </summary>
    public CreateAccessTokenValidator()
    {
        RuleFor(x => x.FormId)
            .GreaterThan(0);

        RuleFor(x => x.SubmissionId)
            .GreaterThan(0);

        RuleFor(x => x.ExpiryMinutes)
            .NotEmpty()
            .GreaterThan(0)
            .LessThanOrEqualTo(10080); // 1 week

        RuleFor(x => x.Permissions)
            .NotEmpty();

        RuleForEach(x => x.Permissions)
            .Must(SubmissionAccessTokenPermissions.IsValid)
            .WithMessage($"Invalid permission. Valid permissions are: {string.Join(", ", SubmissionAccessTokenPermissions.AllNames)}");
    }
}
