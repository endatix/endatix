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
        Summary(s =>
        {
            s.Summary = "List data lists";
            s.Description = "Lists data lists for the current tenant with paging and an optional locale filter.";
            s.ExampleRequest = new DataListsListRequest
            {
                Page = 1,
                PageSize = 20,
                HasLocale = "es"
            };
            s.ResponseExamples[200] = new Paged<DataListModel>(
                page: 1,
                pageSize: 20,
                totalRecords: 1,
                totalPages: 1,
                items:
                [
                    new DataListModel
                    {
                        Id = 1,
                        Name = "Cities",
                        Description = "Major cities used in forms",
                        IsActive = true,
                        CreatedAt = DateTime.UtcNow,
                        ItemsCount = 2,
                        DefaultLocale = "en",
                        AvailableLocales = ["es"]
                    }
                ]);
            s.Responses[200] = "Data lists retrieved successfully.";
            s.Responses[400] = "Invalid input data.";
        });
        Description(builder => builder
            .Produces<Paged<DataListModel>>(StatusCodes.Status200OK, "application/json")
            .ProducesProblem(StatusCodes.Status400BadRequest));
    }

    /// <inheritdoc />
    public override async Task<Results<Ok<Paged<DataListModel>>, ProblemHttpResult>> ExecuteAsync(DataListsListRequest request, CancellationToken ct)
    {
        var result = await mediator.Send(new ListDataListsQuery(request.Page, request.PageSize, request.HasLocale), ct);

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
    /// <summary>
    /// Initializes a new instance of the <see cref="DataListsListValidator"/> class.
    /// </summary>
    public DataListsListValidator()
    {
        Include(new PageableRequestValidator());
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

    /// <summary>
    /// Optional locale code; returns only lists whose AvailableLocales contain this code.
    /// </summary>
    public string? HasLocale { get; set; }
}
