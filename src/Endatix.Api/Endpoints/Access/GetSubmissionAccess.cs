using Endatix.Api.Infrastructure;
using Endatix.Core.Abstractions.Authorization;
using Endatix.Core.Authorization.Access;
using Endatix.Core.Infrastructure.Caching;
using Endatix.Infrastructure.Features.AccessControl;
using FastEndpoints;
using FluentValidation;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Endatix.Api.Endpoints.Access;

/// <summary>
/// Authenticated endpoint for backend management access data for form/submission actions.
/// </summary>
public class GetSubmissionAccess(
    IResourceAccessQuery<SubmissionAccessData, SubmissionManagementAccessContext> submissionManagementAccessPolicy
) : Endpoint<GetSubmissionAccessRequest, Results<Ok<GetSubmissionAccessResponse>, ProblemHttpResult>>
{
    public override void Configure()
    {
        Get("access/forms/{formId}/submissions/{submissionId}");
        Permissions(Actions.Access.Hub);
        Summary(s =>
        {
            s.Summary = "Get submission management access";
            s.Description = "Gets permissions for authenticated backend management operations on forms and submissions.";
            s.Responses[200] = "Permissions retrieved successfully.";
            s.Responses[400] = "Invalid input data.";
            s.Responses[401] = "Authentication required.";
            s.Responses[403] = "Forbidden.";
        });
    }

    public override async Task<Results<Ok<GetSubmissionAccessResponse>, ProblemHttpResult>> ExecuteAsync(
        GetSubmissionAccessRequest request,
        CancellationToken ct)
    {
        var context = new SubmissionManagementAccessContext(request.FormId, request.SubmissionId);
        var accessDataResult = await submissionManagementAccessPolicy.GetAccessData(context, ct);

        return TypedResultsBuilder
            .MapResult(accessDataResult, GetSubmissionAccessResponse.FromCached)
            .SetTypedResults<Ok<GetSubmissionAccessResponse>, ProblemHttpResult>();
    }
}

/// <summary>
/// Request model for getting authenticated submission access.
/// </summary>
public class GetSubmissionAccessRequest
{
    /// <summary>
    /// The form ID (from route).
    /// </summary>
    public long FormId { get; set; }

    /// <summary>
    /// The submission ID (from route).
    /// </summary>
    public long SubmissionId { get; set; }
}

/// <summary>
/// Validator for the <see cref="GetSubmissionAccessRequest"/> model.
/// </summary>
public class GetSubmissionAccessValidator : Validator<GetSubmissionAccessRequest>
{
    public GetSubmissionAccessValidator()
    {
        RuleFor(x => x.FormId)
            .GreaterThan(0);

        RuleFor(x => x.SubmissionId)
            .GreaterThan(0);
    }
}

/// <summary>
/// Response model for getting authenticated submission management access.
/// </summary>
/// <param name="FormId">The form ID.</param>
/// <param name="SubmissionId">The submission ID.</param>
/// <param name="FormPermissions">The form permissions.</param>
/// <param name="SubmissionPermissions">The submission permissions.</param>
/// <param name="CachedAt">The cached at.</param>
/// <param name="ExpiresAt">The expires at.</param>
/// <param name="ETag">The ETag.</param>
public record GetSubmissionAccessResponse(
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
    /// Creates a new instance of the <see cref="GetSubmissionAccessResponse"/> from a cached <see cref="SubmissionAccessData"/>.
    /// </summary>
    /// <param name="cached">The cached <see cref="SubmissionAccessData"/>.</param>
    /// <returns>The <see cref="GetSubmissionAccessResponse"/>.</returns>
    public static GetSubmissionAccessResponse FromCached(Cached<SubmissionAccessData> cached)
        => new(cached.Data.FormId, cached.Data.SubmissionId, cached.Data.FormPermissions, cached.Data.SubmissionPermissions, cached.CachedAt, cached.ExpiresAt, cached.ETag);
}

