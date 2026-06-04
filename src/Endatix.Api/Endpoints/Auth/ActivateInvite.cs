using Endatix.Api.Common.Security;
using Endatix.Api.Infrastructure;
using Endatix.Core.Entities.Identity;
using Endatix.Core.Infrastructure.Logging;
using Endatix.Core.UseCases.Identity.ActivateInvite;
using FastEndpoints;
using FluentValidation;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Endatix.Api.Endpoints.Auth;

/// <summary>
/// Endpoint for activating a tenant invitation. Validates a one-time invitation token, confirms email, and lets the invitee set their password.
/// </summary>
public sealed class ActivateInvite(IMediator mediator)
    : Endpoint<ActivateInviteRequest, Results<Ok<ActivateInviteResponse>, ProblemHttpResult>>
{
    /// <summary>
    /// Configures the endpoint settings.
    /// </summary>
    public override void Configure()
    {
        Post("auth/activate-invite");
        AllowAnonymous();
        Summary(s =>
        {
            s.Summary = "Activate a tenant invitation";
            s.Description = "Validates a one-time invitation token, confirms email, and lets the invitee set their password.";
            s.ExampleRequest = new ActivateInviteRequest
            {
                Token = "invitation-token-value",
                Password = "NewSecurePassword123!",
                ConfirmPassword = "NewSecurePassword123!"
            };
            s.Responses[200] = "Invitation activated successfully.";
            s.Responses[400] = "Invalid input data or invitation token.";
        });
        Description(builder => builder
            .Produces<ActivateInviteResponse>(200, "application/json")
            .ProducesProblem(400));
    }

    /// <inheritdoc/>
    public override async Task<Results<Ok<ActivateInviteResponse>, ProblemHttpResult>> ExecuteAsync(
        ActivateInviteRequest request,
        CancellationToken ct)
    {
        var result = await mediator.Send(
            new ActivateInviteCommand(request.Token, request.Password),
            ct);

        return TypedResultsBuilder
            .MapResult(result, Map)
            .SetErrorMessage("Could not activate invitation.")
            .SetTypedResults<Ok<ActivateInviteResponse>, ProblemHttpResult>();
    }

    private static ActivateInviteResponse Map(User user)
    {
        return new ActivateInviteResponse(
            Success: true,
            Message: "Invitation activated successfully.",
            Email: user.Email);
    }
}

/// <summary>
/// Request to activate a tenant invitation.
/// </summary>
public sealed record ActivateInviteRequest
{
    /// <summary>
    /// The one-time invitation token.
    /// </summary>
    [Sensitive(SensitivityType.Secret)]
    public required string Token { get; init; }

    /// <summary>
    /// The new password to set for the account.
    /// </summary>
    [Sensitive(SensitivityType.Secret)]
    public required string Password { get; init; }

    /// <summary>
    /// Confirmation of the new password.
    /// </summary>
    [Sensitive(SensitivityType.Secret)]
    public required string ConfirmPassword { get; init; }
}

/// <summary>
/// Response returned after activating an invitation.
/// </summary>
/// <param name="Success">Indicates whether the invitation was activated successfully.</param>
/// <param name="Message">A message describing the result.</param>
/// <param name="Email">The email address of the activated user.</param>
public sealed record ActivateInviteResponse(bool Success, string Message, string Email);

/// <summary>
/// Validator for the <see cref="ActivateInviteRequest"/> class.
/// </summary>
public sealed class ActivateInviteValidator : Validator<ActivateInviteRequest>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ActivateInviteValidator"/> class.
    /// </summary>
    public ActivateInviteValidator()
    {
        RuleFor(request => request.Token)
            .NotEmpty()
            .MaximumLength(EmailVerificationToken.MaxRawTokenLength);

        RuleFor(request => request.Password)
            .NotEmpty()
            .SetValidator(new PasswordValidator(nameof(ActivateInviteRequest.Password)));

        RuleFor(request => request.ConfirmPassword)
            .NotEmpty()
            .WithMessage("Confirm your password")
            .Equal(request => request.Password)
            .WithMessage("Passwords do not match");
    }
}
