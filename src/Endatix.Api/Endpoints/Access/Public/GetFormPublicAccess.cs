using Endatix.Api.Infrastructure;
using Endatix.Core.Authorization.Access;
using Endatix.Core.Infrastructure;
using Endatix.Infrastructure.Caching;
using Endatix.Infrastructure.Features.AccessControl;
using FastEndpoints;
using FluentValidation;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Endatix.Api.Endpoints.Access;

/// <summary>
/// Public endpoint for ReBAC (resource based access control) for form/submission related access control
/// on public pages used for submission, prefilling and forms sharing.
/// Uses FormId + Token + TokenType (no SubmissionId; submission is resolved from token).
/// </summary>
public class GetFormPublicAccess(
    IResourceAccessQuery<PublicFormAccessData, PublicFormAccessContext> publicFormAccessPolicy
) : Endpoint<GetFormPublicAccessRequest, Results<Ok<GetFormPublicAccessResponse>, ProblemHttpResult>>
{
    public override void Configure()
    {
        Get("access/public/forms/{formId}");
        AllowAnonymous();
        Summary(s =>
        {
            s.Summary = "Get public form access";
            s.Description = "Gets permissions for a form and its submissions in public/token context (no auth).";
            s.Responses[200] = "Permissions retrieved successfully.";
            s.Responses[400] = "Invalid input data.";
            s.Responses[404] = "Form not found.";
        });
    }

    public override async Task<Results<Ok<GetFormPublicAccessResponse>, ProblemHttpResult>> ExecuteAsync(
        GetFormPublicAccessRequest request,
        CancellationToken ct)
    {
        var context = new PublicFormAccessContext(request.FormId, request.Token, request.TokenType);
        var accessDataResult = await publicFormAccessPolicy.GetAccessData(context, ct);

        return TypedResultsBuilder
            .MapResult(accessDataResult, GetFormPublicAccessResponse.FromCached)
            .SetTypedResults<Ok<GetFormPublicAccessResponse>, ProblemHttpResult>();
    }
}

/// <summary>
/// Request model for getting public form access (anonymous/token).
/// </summary>
public class GetFormPublicAccessRequest
{
    /// <summary>
    /// The form ID (from route).
    /// </summary>
    public long FormId { get; set; }

    /// <summary>
    /// The token (access token or submission token). When set, TokenType must be set.
    /// </summary>
    public string? Token { get; set; }

    /// <summary>
    /// The type of token when Token is provided.
    /// </summary>
    public SubmissionTokenType? TokenType { get; set; }
}

/// <summary>
/// Validator for the <see cref="GetFormPublicAccessRequest"/> model.
/// </summary>
public class GetFormPublicAccessValidator : Validator<GetFormPublicAccessRequest>
{
    public GetFormPublicAccessValidator()
    {
        RuleFor(x => x.FormId)
            .GreaterThan(0);

        RuleFor(x => x.Token)
            .NotEmpty()
            .When(x => x.Token != null);

        RuleFor(x => x.TokenType)
            .NotNull()
            .IsInEnum()
            .When(x => !string.IsNullOrEmpty(x.Token));
    }
}

/// <summary>
/// Response model for getting public form access (anonymous/token).
/// </summary>
/// <param name="FormId">The form ID.</param>
/// <param name="SubmissionId">The submission ID.</param>
/// <param name="FormPermissions">The form permissions.</param>
/// <param name="SubmissionPermissions">The submission permissions.</param>
/// <param name="CachedAt">The cached at.</param>
/// <param name="ExpiresAt">The expires at.</param>
/// <param name="ETag">The ETag.</param>
public record GetFormPublicAccessResponse(
    string FormId,
    string? SubmissionId,
    HashSet<string> FormPermissions,
    HashSet<string> SubmissionPermissions,
    DateTime CachedAt,
    DateTime ExpiresAt,
    string ETag
) : ICachedData
{
    /// <summary>
    /// Creates a new instance of the <see cref="GetFormPublicAccessResponse"/> from a cached <see cref="PublicFormAccessData"/>.
    /// </summary>
    /// <param name="cached">The cached <see cref="PublicFormAccessData"/>.</param>
    /// <returns>The <see cref="GetFormPublicAccessResponse"/>.</returns>
    public static GetFormPublicAccessResponse FromCached(Cached<PublicFormAccessData> cached)
        => new(cached.Data.FormId, cached.Data.SubmissionId, cached.Data.FormPermissions, cached.Data.SubmissionPermissions, cached.CachedAt, cached.ExpiresAt, cached.ETag);
}


