using Endatix.Api.Common;
using Endatix.Api.Infrastructure;
using Endatix.Core.Infrastructure.Paging;
using Endatix.Core.Infrastructure.Result;
using Endatix.Infrastructure.Features.PlatformAdmin.ListPlatformTenants;
using Endatix.Infrastructure.Identity.Authorization;
using FastEndpoints;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Endatix.Api.Endpoints.Admin.Tenants;

/// <summary>
/// Endpoint for listing platform tenants.
/// </summary>
public sealed class List(IListPlatformTenants listPlatformTenants)
    : Endpoint<ListPlatformTenantsRequest, Results<Ok<Paged<PlatformTenantListItem>>, ProblemHttpResult>>
{
    public override void Configure()
    {
        Get("/admin/tenants");
        Policies(AuthorizationPolicies.PlatformAdminAccess);
        Summary(s =>
        {
            s.Summary = "List platform tenants";
            s.Description =
                "Returns a platform-scoped paged list of tenants with optional search, sort, and created/modified date bounds.";
            s.Responses[200] = "Tenants retrieved successfully.";
            s.Responses[400] = "Invalid request.";
        });
    }

    /// <inheritdoc />
    public override async Task<Results<Ok<Paged<PlatformTenantListItem>>, ProblemHttpResult>> ExecuteAsync(
        ListPlatformTenantsRequest request,
        CancellationToken ct)
    {
        var sort = request.ToSortRequest(PlatformTenantListSortBy.Name, SortDirection.Asc);
        var result = await listPlatformTenants.ExecuteAsync(
            request.ResolvePage(),
            request.ResolvePageSize(),
            request.Search,
            sort.Field,
            sort.Direction == SortDirection.Desc,
            request.ToCreatedRange(),
            request.ToModifiedRange(),
            ct);

        return TypedResultsBuilder.FromResult(result)
            .SetTypedResults<Ok<Paged<PlatformTenantListItem>>, ProblemHttpResult>();
    }
}

/// <summary>
/// Request for listing platform tenants.
/// </summary>
public sealed record ListPlatformTenantsRequest :
    ISearchablePagedRequest,
    ISortableRequest<PlatformTenantListSortBy>,
    ICreatedRange,
    IModifiedRange
{
    public int? Page { get; set; }
    public int? PageSize { get; set; }
    public string? Search { get; set; }
    public PlatformTenantListSortBy? SortBy { get; set; }
    public SortDirection? SortDir { get; set; }
    public string? CreatedFrom { get; set; }
    public string? CreatedTo { get; set; }
    public string? ModifiedFrom { get; set; }
    public string? ModifiedTo { get; set; }
}

/// <summary>
/// Validator for listing platform tenants.
/// </summary>
public sealed class ListPlatformTenantsValidator : Validator<ListPlatformTenantsRequest>
{
    public ListPlatformTenantsValidator()
    {
        Include(new SearchablePagedRequestValidator());
        Include(new SortableRequestValidator<PlatformTenantListSortBy>());
        this.RuleForCalendarDayRange(x => x.CreatedFrom, x => x.CreatedTo, "CreatedFrom");
        this.RuleForCalendarDayRange(x => x.ModifiedFrom, x => x.ModifiedTo, "ModifiedFrom");
    }
}
