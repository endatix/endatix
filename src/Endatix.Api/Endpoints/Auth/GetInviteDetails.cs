using Endatix.Api.Infrastructure;
using Endatix.Core.Abstractions;
using Endatix.Core.Entities.Identity;
using Endatix.Core.Infrastructure.Logging;
using FastEndpoints;
using FluentValidation;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Endatix.Api.Endpoints.Auth;

/// <summary>
/// Endpoint for getting tenant invitation details. Validates a one-time invitation token without consuming it and returns details needed by the activation page.
/// </summary>
public sealed class GetInviteDetails(IEmailVerificationService emailVerificationService)
    : Endpoint<GetInviteDetailsRequest, Results<Ok<GetInviteDetailsResponse>, ProblemHttpResult>>
{
    /// <summary>
    /// Configures the endpoint settings.
    /// </summary>
    public override void Configure()
    {
        Post("auth/activate-invite/details");
        AllowAnonymous();
        Summary(s =>
        {
            s.Summary = "Get tenant invitation details";
            s.Description = "Validates a one-time invitation token without consuming it and returns details needed by the activation page.";
            s.ExampleRequest = new GetInviteDetailsRequest
            {
                Token = "invitation-token-value"
            };
            s.Responses[200] = "Invitation details returned successfully.";
            s.Responses[400] = "Invalid or expired invitation token.";
        });
        Description(builder => builder
            .Produces<GetInviteDetailsResponse>(200, "application/json")
            .ProducesProblem(400));
    }

    /// <inheritdoc/>
    public override async Task<Results<Ok<GetInviteDetailsResponse>, ProblemHttpResult>> ExecuteAsync(
        GetInviteDetailsRequest request,
        CancellationToken cancellationToken)
    {
        var result = await emailVerificationService.GetPendingInviteUserAsync(
            request.Token,
            cancellationToken);

        return TypedResultsBuilder
            .MapResult(result, Map)
            .SetErrorMessage("Could not load invitation details.")
            .SetTypedResults<Ok<GetInviteDetailsResponse>, ProblemHttpResult>();
    }

    private static GetInviteDetailsResponse Map(User user)
    {
        return new GetInviteDetailsResponse(user.Email);
    }
}

/// <summary>
/// Request to get tenant invitation details.
/// </summary>
public sealed record GetInviteDetailsRequest
{
    /// <summary>
    /// The one-time invitation token.
    /// </summary>
    [Sensitive(SensitivityType.Secret)]
    public required string Token { get; init; }
}

/// <summary>
/// Response returned with tenant invitation details.
/// </summary>
/// <param name="Email">The email address associated with the invitation.</param>
public sealed record GetInviteDetailsResponse(string Email);

/// <summary>
/// Validator for the <see cref="GetInviteDetailsRequest"/> class.
/// </summary>
public sealed class GetInviteDetailsValidator : Validator<GetInviteDetailsRequest>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="GetInviteDetailsValidator"/> class.
    /// </summary>
    public GetInviteDetailsValidator()
    {
        RuleFor(request => request.Token)
            .NotEmpty();
    }
}
