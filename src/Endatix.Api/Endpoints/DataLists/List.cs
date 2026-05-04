using Endatix.Api.Common;
using Endatix.Api.Infrastructure;
using Endatix.Core.Abstractions.Authorization;
using Endatix.Core.Infrastructure.Result;
using Endatix.Core.UseCases.DataLists.List;
using FastEndpoints;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Endatix.Api.Endpoints.DataLists;

/// <summary>
/// Endpoint to list data lists.
/// </summary>
public sealed class List(
    IMediator mediator)
    : Endpoint<DataListsListRequest, Results<Ok<Paged<DataListModel>>, ProblemHttpResult>>
{
    /// <summary>
    /// Configures the endpoint settings.
    /// </summary>
    public override void Configure()
    {
        Get("data-lists");
        Permissions(Actions.Forms.View);
    }
    /// <summary>
    /// Executes the endpoint.
    /// </summary>
    /// <param name="request">The request.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>The result.</returns>
    public override async Task<Results<Ok<Paged<DataListModel>>, ProblemHttpResult>> ExecuteAsync(DataListsListRequest request, CancellationToken ct)
    {
        var result = await mediator.Send(new ListDataListsQuery(request.Page, request.PageSize), ct);

        if (!result.IsSuccess)
        {
            return result.ToProblem();
        }

        var mapped = result.Value.MapToPaged(DataListMapper.Map);

        return TypedResults.Ok(mapped);
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
    /// <inheritdoc />
    public int? Page { get; set; }

    /// <inheritdoc />
    public int? PageSize { get; set; }
}

