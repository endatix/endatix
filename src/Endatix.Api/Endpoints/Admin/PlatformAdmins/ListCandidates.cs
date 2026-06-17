using Endatix.Api.Common;
using Endatix.Api.Infrastructure;
using Endatix.Core.Abstractions.Authorization;
using Endatix.Core.Infrastructure.Result;
using Endatix.Infrastructure.Features.PlatformAdmin.ListPlatformAdminCandidates;
using FastEndpoints;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Endatix.Api.Endpoints.Admin.PlatformAdmins;

/// <summary>
/// Endpoint for listing platform administrator candidates.
/// </summary>
public sealed class ListCandidates(ListPlatformAdminCandidates listPlatformAdminCandidates)
    : Endpoint<ListPlatformAdminCandidatesRequest, Results<Ok<Paged<PlatformAdminUserResponse>>, ProblemHttpResult>>
{
    public override void Configure()
    {
        Get("/admin/platform-admins/candidates");
        Policies(SystemRole.PlatformAdmin.Name);
        Summary(s =>
        {
            s.Summary = "List platform administrator candidates";
            s.Description = "Returns users who can be locally approved as platform administrators.";
            s.Responses[200] = "Candidates retrieved successfully.";
            s.Responses[400] = "Invalid request.";
        });
    }

    /// <inheritdoc />
    public override async Task<Results<Ok<Paged<PlatformAdminUserResponse>>, ProblemHttpResult>> ExecuteAsync(
        ListPlatformAdminCandidatesRequest request,
        CancellationToken ct)
    {
        var result = await listPlatformAdminCandidates.ExecuteAsync(
            request.ResolvePage(),
            request.ResolvePageSize(),
            request.Search,
            ct);

        return TypedResultsBuilder
            .MapResult(result, PlatformAdminUserResponse.MapPage)
            .SetTypedResults<Ok<Paged<PlatformAdminUserResponse>>, ProblemHttpResult>();
    }
}

/// <summary>
/// Request for listing platform administrator candidates.
/// </summary>
public sealed record ListPlatformAdminCandidatesRequest : ISearchablePagedRequest
{
    public int? Page { get; set; }
    public int? PageSize { get; set; }
    public string? Search { get; set; }
}

/// <summary>
/// Validator for listing platform administrator candidates.
/// </summary>
public sealed class ListPlatformAdminCandidatesValidator : Validator<ListPlatformAdminCandidatesRequest>
{
    public ListPlatformAdminCandidatesValidator()
    {
        Include(new SearchablePagedRequestValidator());
    }
}
