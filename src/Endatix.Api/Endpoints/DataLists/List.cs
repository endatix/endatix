using System.Globalization;
using Endatix.Api.Common;
using Endatix.Api.Endpoints.Common;
using Endatix.Api.Infrastructure;
using Endatix.Core.Abstractions.Authorization;
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
                SortBy = "name",
                SortDir = "asc"
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
        var result = await mediator.Send(
            new ListDataListsQuery(
                request.Page,
                request.PageSize,
                request.HasLocale,
                request.Search,
                ParseSortBy(request.SortBy),
                ParseSortDescending(request.SortDir),
                ParseInclusiveDayStartUtc(request.CreatedFrom),
                ParseExclusiveDayEndUtc(request.CreatedTo),
                ParseInclusiveDayStartUtc(request.ModifiedFrom),
                ParseExclusiveDayEndUtc(request.ModifiedTo)),
            ct);

        if (!result.IsSuccess)
        {
            return result.ToProblem();
        }

        var mapped = result.Value.MapToPaged(DataListMapper.Map);

        return TypedResults.Ok(mapped);
    }

    internal static DataListListSortBy ParseSortBy(string? sortBy) =>
        sortBy?.Trim().ToLowerInvariant() switch
        {
            "name" => DataListListSortBy.Name,
            "modifiedat" => DataListListSortBy.ModifiedAt,
            "itemscount" => DataListListSortBy.ItemsCount,
            "isactive" => DataListListSortBy.IsActive,
            "createdat" => DataListListSortBy.CreatedAt,
            _ => DataListListSortBy.CreatedAt
        };

    internal static bool ParseSortDescending(string? sortDir) =>
        !string.Equals(sortDir?.Trim(), "asc", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Parses a UTC calendar date (<c>YYYY-MM-DD</c>) as the inclusive start of that day.
    /// </summary>
    internal static DateTime? ParseInclusiveDayStartUtc(string? value)
    {
        if (!TryParseUtcCalendarDate(value, out DateOnly day))
        {
            return null;
        }

        return DateTime.SpecifyKind(day.ToDateTime(TimeOnly.MinValue), DateTimeKind.Utc);
    }

    /// <summary>
    /// Parses a UTC calendar date (<c>YYYY-MM-DD</c>) as the exclusive end (start of next day).
    /// </summary>
    internal static DateTime? ParseExclusiveDayEndUtc(string? value)
    {
        if (!TryParseUtcCalendarDate(value, out DateOnly day))
        {
            return null;
        }

        // DateOnly.MaxValue (9999-12-31) has no "next day" to represent the
        // exclusive upper bound; clamp to DateTime.MaxValue instead of letting
        // AddDays(1) throw ArgumentOutOfRangeException.
        if (day == DateOnly.MaxValue)
        {
            return DateTime.SpecifyKind(DateTime.MaxValue, DateTimeKind.Utc);
        }

        return DateTime.SpecifyKind(day.AddDays(1).ToDateTime(TimeOnly.MinValue), DateTimeKind.Utc);
    }

    private static bool TryParseUtcCalendarDate(string? value, out DateOnly day)
    {
        day = default;
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        return DateOnly.TryParseExact(
            value.Trim(),
            "yyyy-MM-dd",
            CultureInfo.InvariantCulture,
            DateTimeStyles.None,
            out day);
    }
}

/// <summary>
/// Validator for the DataListsListRequest.
/// </summary>
public sealed class DataListsListValidator : Validator<DataListsListRequest>
{
    private static readonly string[] AllowedSortBy =
    [
        "name",
        "createdAt",
        "modifiedAt",
        "itemsCount",
        "isActive"
    ];

    private static readonly string[] AllowedSortDir = ["asc", "desc"];

    /// <summary>
    /// Initializes a new instance of the <see cref="DataListsListValidator"/> class.
    /// </summary>
    public DataListsListValidator()
    {
        Include(new SearchablePagedRequestValidator());
        RuleFor(x => x.HasLocale)
            .IsHasLocaleFilter()
            .When(x => !string.IsNullOrWhiteSpace(x.HasLocale));
        RuleFor(x => x.SortBy)
            .Must(value => AllowedSortBy.Contains(value!, StringComparer.OrdinalIgnoreCase))
            .When(x => !string.IsNullOrWhiteSpace(x.SortBy))
            .WithMessage("SortBy must be one of: name, createdAt, modifiedAt, itemsCount, isActive.");
        RuleFor(x => x.SortDir)
            .Must(value => AllowedSortDir.Contains(value!, StringComparer.OrdinalIgnoreCase))
            .When(x => !string.IsNullOrWhiteSpace(x.SortDir))
            .WithMessage("SortDir must be asc or desc.");
        RuleFor(x => x.CreatedFrom)
            .Must(BeCalendarDate)
            .When(x => !string.IsNullOrWhiteSpace(x.CreatedFrom))
            .WithMessage("CreatedFrom must be a UTC calendar date (YYYY-MM-DD).");
        RuleFor(x => x.CreatedTo)
            .Must(BeCalendarDate)
            .When(x => !string.IsNullOrWhiteSpace(x.CreatedTo))
            .WithMessage("CreatedTo must be a UTC calendar date (YYYY-MM-DD).");
        RuleFor(x => x.ModifiedFrom)
            .Must(BeCalendarDate)
            .When(x => !string.IsNullOrWhiteSpace(x.ModifiedFrom))
            .WithMessage("ModifiedFrom must be a UTC calendar date (YYYY-MM-DD).");
        RuleFor(x => x.ModifiedTo)
            .Must(BeCalendarDate)
            .When(x => !string.IsNullOrWhiteSpace(x.ModifiedTo))
            .WithMessage("ModifiedTo must be a UTC calendar date (YYYY-MM-DD).");
    }

    private static bool BeCalendarDate(string? value) =>
        List.ParseInclusiveDayStartUtc(value).HasValue;
}

/// <summary>
/// Request to list data lists.
/// </summary>
public sealed class DataListsListRequest : ISearchablePagedRequest
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

    /// <summary>
    /// Optional sort field: name, createdAt, modifiedAt, itemsCount, isActive.
    /// </summary>
    public string? SortBy { get; set; }

    /// <summary>
    /// Optional sort direction: asc or desc (default desc).
    /// </summary>
    public string? SortDir { get; set; }

    /// <summary>
    /// Inclusive created-at UTC calendar day (<c>YYYY-MM-DD</c>).
    /// </summary>
    public string? CreatedFrom { get; set; }

    /// <summary>
    /// Inclusive created-at UTC calendar day (<c>YYYY-MM-DD</c>).
    /// </summary>
    public string? CreatedTo { get; set; }

    /// <summary>
    /// Inclusive modified-at UTC calendar day (<c>YYYY-MM-DD</c>).
    /// </summary>
    public string? ModifiedFrom { get; set; }

    /// <summary>
    /// Inclusive modified-at UTC calendar day (<c>YYYY-MM-DD</c>).
    /// </summary>
    public string? ModifiedTo { get; set; }
}
