using Endatix.Api.Infrastructure;
using Endatix.Core.Abstractions.Authorization;
using Endatix.Core.Authorization.Access;
using Endatix.Infrastructure.Caching;
using Endatix.Infrastructure.Features.AccessControl;
using FastEndpoints;
using FluentValidation;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Endatix.Api.Endpoints.Access;

/// <summary>
/// Authenticated endpoint for backend/admin management access data for forms.
/// </summary>
public sealed class GetFormAccess(
    IResourceAccessQuery<FormAccessData, FormAccessContext> formAccessPolicy
) : Endpoint<GetFormAccessRequest, Results<Ok<GetFormAccessResponse>, ProblemHttpResult>>
{
    public override void Configure()
    {
        Get("access/forms/{formId}");
        Permissions(Actions.Access.Hub);
        Summary(s =>
        {
            s.Summary = "Get form management access";
            s.Description = "Gets permissions for authenticated backend management operations on forms (and their submissions scope).";
            s.Responses[200] = "Permissions retrieved successfully.";
            s.Responses[400] = "Invalid input data.";
            s.Responses[401] = "Authentication required.";
            s.Responses[403] = "Forbidden.";
        });
    }

    public override async Task<Results<Ok<GetFormAccessResponse>, ProblemHttpResult>> ExecuteAsync(
        GetFormAccessRequest request,
        CancellationToken ct)
    {
        var context = new FormAccessContext(request.FormId);
        var accessDataResult = await formAccessPolicy.GetAccessData(context, ct);

        return TypedResultsBuilder
            .MapResult(accessDataResult, GetFormAccessResponse.FromCached)
            .SetTypedResults<Ok<GetFormAccessResponse>, ProblemHttpResult>();
    }
}

/// <summary>
/// Request model for getting authenticated form management access.
/// </summary>
public sealed class GetFormAccessRequest
{
    /// <summary>
    /// The form ID (from route).
    /// </summary>
    public long FormId { get; set; }
}

/// <summary>
/// Validator for the <see cref="GetFormAccessRequest"/> model.
/// </summary>
public sealed class GetFormAccessValidator : Validator<GetFormAccessRequest>
{
    public GetFormAccessValidator()
    {
        RuleFor(x => x.FormId)
            .GreaterThan(0);
    }
}

/// <summary>
/// Response model for getting authenticated form management access.
/// </summary>
public sealed record GetFormAccessResponse(
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
    /// Creates a new instance of the <see cref="GetFormAccessResponse"/> from a cached <see cref="FormAccessData"/>.
    /// </summary>
    public static GetFormAccessResponse FromCached(Cached<FormAccessData> cached)
        => new(
            cached.Data.FormId,
            cached.Data.SubmissionId,
            cached.Data.FormPermissions,
            cached.Data.SubmissionPermissions,
            cached.CachedAt,
            cached.ExpiresAt,
            cached.ETag);
}

