using Endatix.Api.Infrastructure;
using Endatix.Core.Abstractions.Authorization;
using Endatix.Core.Authorization.Access;
using Endatix.Core.Infrastructure;
using Endatix.Infrastructure.Caching;
using Endatix.Infrastructure.Features.AccessControl;
using FastEndpoints;
using FluentValidation;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Endatix.Api.Endpoints.Access;

/// <summary>
/// Authenticated endpoint for backend/admin management access data for form templates.
/// </summary>
public sealed class GetFormTemplateAccess(
    IResourceAccessQuery<FormTemplateAccessData, FormTemplateAccessContext> templateAccessPolicy
) : Endpoint<GetFormTemplateAccessRequest, Results<Ok<GetFormTemplateAccessResponse>, ProblemHttpResult>>
{
    public override void Configure()
    {
        Get("access/form-templates/{templateId}");
        Permissions(Actions.Access.Hub);

        Summary(s =>
        {
            s.Summary = "Get form template management access";
            s.Description = "Gets permissions for authenticated backend management operations on form templates.";
            s.Responses[200] = "Permissions retrieved successfully.";
            s.Responses[400] = "Invalid input data.";
            s.Responses[401] = "Authentication required.";
            s.Responses[403] = "Forbidden.";
        });
    }

    public override async Task<Results<Ok<GetFormTemplateAccessResponse>, ProblemHttpResult>> ExecuteAsync(
        GetFormTemplateAccessRequest request,
        CancellationToken ct)
    {
        var context = new FormTemplateAccessContext(request.TemplateId);
        var accessDataResult = await templateAccessPolicy.GetAccessData(context, ct);

        return TypedResultsBuilder
            .MapResult(accessDataResult, GetFormTemplateAccessResponse.FromCached)
            .SetTypedResults<Ok<GetFormTemplateAccessResponse>, ProblemHttpResult>();
    }
}

/// <summary>
/// Request model for getting authenticated form template management access.
/// </summary>
public sealed class GetFormTemplateAccessRequest
{
    /// <summary>
    /// The form template ID (from route).
    /// </summary>
    public long TemplateId { get; set; }
}

/// <summary>
/// Validator for the <see cref="GetFormTemplateAccessRequest"/> model.
/// </summary>
public sealed class GetFormTemplateAccessValidator : Validator<GetFormTemplateAccessRequest>
{
    public GetFormTemplateAccessValidator()
    {
        RuleFor(x => x.TemplateId)
            .GreaterThan(0);
    }
}

/// <summary>
/// Response model for getting authenticated form template management access.
/// </summary>
public sealed record GetFormTemplateAccessResponse(
    string TemplateId,
    HashSet<string> Permissions,
    DateTime CachedAt,
    DateTime ExpiresAt,
    string ETag
) : ICachedData
{
    /// <summary>
    /// Creates a new instance of the <see cref="GetFormTemplateAccessResponse"/> from a cached <see cref="FormTemplateAccessData"/>.
    /// </summary>
    public static GetFormTemplateAccessResponse FromCached(Cached<FormTemplateAccessData> cached)
        => new(cached.Data.TemplateId, cached.Data.Permissions, cached.CachedAt, cached.ExpiresAt, cached.ETag);
}

