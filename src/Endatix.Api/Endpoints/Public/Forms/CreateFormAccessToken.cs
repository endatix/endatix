using Endatix.Api.Common;
using Endatix.Api.Infrastructure;
using Endatix.Core.Authorization.Access;
using Endatix.Core.UseCases.Authorization.PublicForm;
using FastEndpoints;
using FluentValidation;
using MediatR;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Endatix.Api.Endpoints.Public.Forms;

/// <summary>
/// Issue a short-lived JWT for public data list search/display-value endpoints.
/// </summary>
public sealed class CreateFormAccessToken(IMediator mediator)
    : Endpoint<CreateFormAccessTokenRequest, Results<Ok<CreateFormAccessTokenResponse>, ProblemHttpResult>>
{
    /// <inheritdoc />
    public override void Configure()
    {
        Post("forms/{formId}/access-tokens");
        Group<PublicApiGroup>();
        AllowAnonymous();
        Summary(s =>
        {
            s.Summary = "Create form access token";
            s.Description =
                "Returns a JWT for Forms based access control (ReBAC). Requires the form id to be provided in the route.";
            s.Responses[200] = "Form access token created successfully.";
            s.Responses[400] = "Invalid request or access data.";
            s.Responses[404] = "Form not found.";
        });
    }

    /// <inheritdoc />
    public override async Task<Results<Ok<CreateFormAccessTokenResponse>, ProblemHttpResult>> ExecuteAsync(
        CreateFormAccessTokenRequest request,
        CancellationToken ct)
    {
        CreateFormAccessTokenCommand command = new(request.FormId, request.Token, request.TokenType);

        var result = await mediator.Send(command, ct).ConfigureAwait(false);

        return TypedResultsBuilder
            .MapResult(result, static dto => new CreateFormAccessTokenResponse(dto.Token, dto.ExpiresAtUtc))
            .SetTypedResults<Ok<CreateFormAccessTokenResponse>, ProblemHttpResult>();
    }
}

/// <summary>
/// Optional body when minting a form access token (form id comes from the route).
/// </summary>
public sealed class CreateFormAccessTokenRequest
{
    /// <summary>
    /// Route: form id.
    /// </summary>
    public long FormId { get; set; }

    /// <summary>
    /// Optional token for private form access (same as public form access endpoint).
    /// </summary>
    public string? Token { get; set; }

    /// <summary>
    /// Optional token type when <see cref="Token"/> is set.
    /// </summary>
    public SubmissionTokenType? TokenType { get; set; }
}

/// <summary>
/// Issued token response.
/// </summary>
public sealed record CreateFormAccessTokenResponse(string Token, DateTime ExpiresAtUtc);

/// <summary>
/// Validates <see cref="CreateFormAccessTokenRequest"/>.
/// </summary>
public sealed class CreateFormAccessTokenValidator : Validator<CreateFormAccessTokenRequest>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="CreateFormAccessTokenValidator"/> class.
    /// </summary>
    public CreateFormAccessTokenValidator()
    {
        RuleFor(x => x.FormId).GreaterThan(0);

        RuleFor(x => x.Token)
            .NotEmpty()
            .When(x => x.TokenType is not null);

        RuleFor(x => x.TokenType)
            .NotNull()
            .IsInEnum()
            .When(x => !string.IsNullOrEmpty(x.Token));

        RuleFor(x => x.TokenType)
            .Null()
            .When(x => string.IsNullOrEmpty(x.Token))
            .WithMessage("Token must be provided when Token Type is specified.");

        RuleFor(x => x.TokenType)
            .Must(t => t is not SubmissionTokenType.FormToken)
            .WithMessage("FormToken cannot be used to issue form access tokens.");
    }
}
