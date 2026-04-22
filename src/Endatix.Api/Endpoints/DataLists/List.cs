using Endatix.Api.Common;
using Endatix.Api.Common.FeatureFlags;
using Endatix.Api.Infrastructure;
using Endatix.Core.Abstractions.Authorization;
using Endatix.Core.UseCases.DataLists.List;
using Endatix.Framework.FeatureFlags;
using FastEndpoints;
using MediatR;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Endatix.Api.Endpoints.DataLists;

/// <summary>
/// Endpoint to list data lists.
/// </summary>
public sealed class List(
    IMediator mediator)
    : Endpoint<DataListsListRequest, Results<Ok<IEnumerable<DataListModel>>, ProblemHttpResult>>
{
    public override void Configure()
    {
        Get("data-lists");
        Permissions(Actions.Forms.View);
        FeatureFlag<EndpointFeatureGate>(FeatureFlags.DataLists);
    }

    public override async Task<Results<Ok<IEnumerable<DataListModel>>, ProblemHttpResult>> ExecuteAsync(DataListsListRequest request, CancellationToken ct)
    {
        var result = await mediator.Send(new ListDataListsQuery(request.Page, request.PageSize), ct);

        return TypedResultsBuilder
            .MapResult(result, dataLists => dataLists.Select(DataListMapper.Map))
            .SetTypedResults<Ok<IEnumerable<DataListModel>>, ProblemHttpResult>();
    }
}

/// <summary>
/// Validator for the DataListsListRequest.
/// </summary>
public sealed class DataListsListValidator : Validator<DataListsListRequest>
{
    public DataListsListValidator()
    {
        Include(new PagedRequestValidator());
    }
}

/// <summary>
/// Request to list data lists.
/// </summary>
public sealed class DataListsListRequest : IPagedRequest
{
    public int? Page { get; set; }
    public int? PageSize { get; set; }
}

