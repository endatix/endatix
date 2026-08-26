using Endatix.Api.Common;
using Endatix.Api.Endpoints.Common;
using Endatix.Api.Infrastructure;
using Endatix.Core.Abstractions.Authorization;
using Endatix.Core.Infrastructure.Paging;
using Endatix.Core.Infrastructure.Result;
using Endatix.Core.UseCases.DataLists.List;
using FastEndpoints;
using FluentValidation;
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
            s.Description =
                "Lists data lists for the current tenant with paging, optional name/description search, " +
                "locale filter, sort, and created/modified date bounds.";
            s.ExampleRequest = new DataListsListRequest
            {
                Page = 1,
                PageSize = 20,
                Search = "cities",
                HasLocale = "es,de",
                SortBy = DataListListSortBy.Name,
                SortDir = SortDirection.Asc
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
    public override async Task<Results<Ok<Paged<DataListModel>>, ProblemHttpResult>> ExecuteAsync(
        DataListsListRequest request,
        CancellationToken ct)
    {
        var sort = request.ToSortRequest(DataListListSortBy.CreatedAt, SortDirection.Desc);
        var result = await mediator.Send(
            new ListDataListsQuery(
                request.Page,
                request.PageSize,
                request.HasLocale,
                request.Search,
                sort.Field,
                sort.Direction == SortDirection.Desc,
                request.ToCreatedRange(),
                request.ToModifiedRange()),
            ct);

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
        Include(new SearchablePagedRequestValidator());
        Include(new SortableRequestValidator<DataListListSortBy>());
        this.RuleForCalendarDayRange(x => x.CreatedFrom, x => x.CreatedTo, "CreatedFrom");
        this.RuleForCalendarDayRange(x => x.ModifiedFrom, x => x.ModifiedTo, "ModifiedFrom");
        RuleFor(x => x.HasLocale)
            .IsHasLocaleFilter()
            .When(x => !string.IsNullOrWhiteSpace(x.HasLocale));
    }
}

/// <summary>
/// Request to list data lists.
/// </summary>
public sealed class DataListsListRequest :
    ISearchablePagedRequest,
    ISortableRequest<DataListListSortBy>,
    ICreatedRange,
    IModifiedRange
{
    /// <inheritdoc />
    public int? Page { get; set; }

    /// <inheritdoc />
    public int? PageSize { get; set; }

    /// <inheritdoc />
    public string? Search { get; set; }

    /// <summary>
    /// Optional culture code or comma-separated list (e.g. <c>es</c> or <c>es,de</c>).
    /// Returns lists whose AvailableLocales contain any code or whose DefaultLocale equals any code.
    /// </summary>
    public string? HasLocale { get; set; }

    /// <inheritdoc />
    public DataListListSortBy? SortBy { get; set; }

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
