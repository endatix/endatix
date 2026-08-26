using Endatix.Api.Common;
using Endatix.Api.Endpoints.Common;
using Endatix.Api.Infrastructure;
using Endatix.Core.Abstractions.Authorization;
using Endatix.Core.Common.Translations;
using Endatix.Core.Infrastructure.Paging;
using Endatix.Core.Infrastructure.Result;
using Endatix.Core.UseCases.DataLists.Search;
using FastEndpoints;
using FluentValidation;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Endatix.Api.Endpoints.DataLists;

/// <summary>
/// Endpoint to list/search data list items for Hub management (authenticated).
/// </summary>
public sealed class ListItems(IMediator mediator)
    : Endpoint<ListDataListItemsRequest, Results<Ok<Paged<DataListItemModel>>, ProblemHttpResult>>
{
    public const int DefaultPageSize = 25;

    /// <inheritdoc />
    public override void Configure()
    {
        Get("data-lists/{dataListId}/items");
        Permissions(Actions.Forms.View);
        Summary(s =>
        {
            s.Summary = "List data list items";
            s.Description =
                "Paged Hub management search of data list items. Matches value and label keys " +
                "(always including Labels.default, plus locale / includeLocales). Inactive lists are included. " +
                "Supports sort and created/modified date bounds.";
            s.ExampleRequest = new ListDataListItemsRequest
            {
                DataListId = 1,
                Query = "york",
                Page = 1,
                PageSize = 25,
                Locale = "es",
                IncludeLocales = ["es", "fr"],
                SortBy = DataListItemListSortBy.Label,
                SortDir = SortDirection.Asc
            };
            s.Responses[200] = "Data list items retrieved successfully.";
            s.Responses[400] = "Invalid input data.";
            s.Responses[404] = "Data list not found.";
        });
        Description(builder => builder
            .Produces<Paged<DataListItemModel>>(StatusCodes.Status200OK, "application/json")
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound));
    }

    /// <inheritdoc />
    public override async Task<Results<Ok<Paged<DataListItemModel>>, ProblemHttpResult>> ExecuteAsync(
        ListDataListItemsRequest request,
        CancellationToken ct)
    {
        var paging = new PageRequest(request.Page, request.PageSize ?? DefaultPageSize);
        var sort = request.ToNullableSortRequest(DataListItemListSortBy.Label, SortDirection.Asc);

        SearchDataListItemsQuery query = new(
            request.DataListId,
            request.Query,
            paging.Skip,
            paging.PageSize,
            new SearchDataListItemsOptions(
                request.MatchMode,
                request.Locale,
                request.IncludeLocales,
                RequireActive: false,
                SortBy: sort?.Field,
                SortDescending: sort?.IsDescending ?? false,
                Created: request.ToCreatedRange(),
                Modified: request.ToModifiedRange()));
        var result = await mediator.Send(query, ct);
        if (!result.IsSuccess)
        {
            return result.ToProblem();
        }

        var mapped = result.Value.MapToPaged(DataListMapper.Map);
        return TypedResults.Ok(mapped);
    }
}

/// <summary>
/// Request to list/search data list items.
/// </summary>
public sealed class ListDataListItemsRequest :
    IPagedRequest,
    ISortableRequest<DataListItemListSortBy>,
    ICreatedRange,
    IModifiedRange
{
    /// <summary>
    /// The ID of the data list.
    /// </summary>
    public long DataListId { get; init; }

    /// <summary>
    /// Free-text query against value and searched label keys.
    /// </summary>
    public string? Query { get; init; }

    /// <summary>
    /// Match mode for <see cref="Query"/>. Defaults to Contains.
    /// </summary>
    public DataListSearchMatchMode MatchMode { get; init; } = DataListSearchMatchMode.Contains;

    /// <summary>
    /// Optional locale for which label key to prefer for ordering (and to include in search).
    /// </summary>
    public string? Locale { get; init; }

    /// <summary>
    /// Extra locales to search and return in <c>labels</c>.
    /// </summary>
    public IReadOnlyCollection<string> IncludeLocales { get; init; } = [];

    /// <inheritdoc />
    public int? Page { get; set; }

    /// <inheritdoc />
    public int? PageSize { get; set; }

    /// <inheritdoc />
    public DataListItemListSortBy? SortBy { get; set; }

    /// <inheritdoc />
    public SortDirection? SortDir { get; set; }

    /// <inheritdoc />
    public string? CreatedFrom { get; set; }

    /// <inheritdoc />
    public string? CreatedTo { get; set; }

    /// <inheritdoc />
    public string? ModifiedFrom { get; set; }

    /// <inheritdoc />
    public string? ModifiedTo { get; set; }
}

/// <summary>
/// Validator for <see cref="ListDataListItemsRequest"/>.
/// </summary>
public sealed class ListDataListItemsValidator : Validator<ListDataListItemsRequest>
{
    public ListDataListItemsValidator()
    {
        Include(new PageableRequestValidator());
        Include(new SortableRequestValidator<DataListItemListSortBy>());
        Include(new CreatedRangeValidator());
        Include(new ModifiedRangeValidator());
        RuleFor(x => x.DataListId).GreaterThan(0);
        RuleFor(x => x.Query)
            .MaximumLength(PagedRequestLimits.MAX_SEARCH_LENGTH)
            .When(x => !string.IsNullOrWhiteSpace(x.Query));
        RuleFor(x => x.MatchMode).IsInEnum();
        RuleFor(x => x.Locale)
            .Must(locale => CultureCode.TryParse(locale, out _))
            .WithMessage("Locale must be a valid culture code (e.g. 'es' or 'en-US') or 'default'.")
            .When(x => !string.IsNullOrWhiteSpace(x.Locale));
        RuleFor(x => x.IncludeLocales)
            .IsIncludeLocales()
            .When(x => x.IncludeLocales is { Count: > 0 });
        RuleFor(x => x.PageSize)
            .LessThanOrEqualTo(SearchDataListItemsQuery.MaxTake)
            .When(x => x.PageSize.HasValue);
    }
}
