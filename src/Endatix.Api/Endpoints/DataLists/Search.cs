using Endatix.Api.Common.FeatureFlags;
using Endatix.Api.Infrastructure;
using Endatix.Core.UseCases.DataLists.Search;
using Endatix.Framework.FeatureFlags;
using FastEndpoints;
using FluentValidation;
using MediatR;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Endatix.Api.Endpoints.DataLists;

/// <summary>
/// Endpoint to search data list items.
/// </summary>
public sealed class Search(
    IMediator mediator)
    : Endpoint<SearchDataListItemsRequest, Results<Ok<DataListPublicSearchResultModel>, ProblemHttpResult>>
{
    public override void Configure()
    {
        Get(ApiRoutes.Public("data-lists/{dataListId}/search"));
        AllowAnonymous();
        Summary(s =>
        {
            s.Summary = "Search data list items";
            s.Description = "Searches public data list choices using simple text matching.";
        });
        FeatureFlag<EndpointFeatureGate>(FeatureFlags.DataLists);
    }

    public override async Task<Results<Ok<DataListPublicSearchResultModel>, ProblemHttpResult>> ExecuteAsync(SearchDataListItemsRequest request, CancellationToken cancellationToken)
    {
        SearchDataListItemsQuery query = new(request.DataListId, request.Query, request.Skip, request.Take);
        var result = await mediator.Send(query, cancellationToken);
        return TypedResultsBuilder
            .MapResult(result, DataListMapper.MapPublic)
            .SetTypedResults<Ok<DataListPublicSearchResultModel>, ProblemHttpResult>();
    }
}


/// <summary>
/// Request to search data list items.
/// </summary>
public sealed class SearchDataListItemsRequest
{

    /// <summary>
    /// The ID of the data list to search.
    /// </summary>
    public long DataListId { get; init; }

    /// <summary>
    /// The query to search for.
    /// </summary>
    public string? Query { get; init; }

    /// <summary>
    /// The number of items to skip.
    /// </summary>
    public int Skip { get; init; } = 0;

    /// <summary>   
    /// The number of items to take.
    /// </summary>
    public int Take { get; init; } = 25;
}


/// <summary>
/// Validator for the SearchDataListItemsRequest.
/// </summary>
public sealed class SearchDataListItemsValidator : Validator<SearchDataListItemsRequest>
{
    public SearchDataListItemsValidator()
    {
        RuleFor(x => x.DataListId).GreaterThan(0);
        RuleFor(x => x.Skip).GreaterThanOrEqualTo(0);
        RuleFor(x => x.Take).GreaterThan(0).LessThanOrEqualTo(SearchDataListItemsQuery.MAX_TAKE);
    }
}

