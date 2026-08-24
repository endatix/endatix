using Ardalis.GuardClauses;
using Endatix.Core.Common.Translations;
using Endatix.Core.Infrastructure.Messaging;
using Endatix.Core.Infrastructure.Paging;
using Endatix.Core.Infrastructure.Result;

namespace Endatix.Core.UseCases.DataLists.Search;

/// <summary>
/// Optional search behavior for <see cref="SearchDataListItemsQuery"/>.
/// </summary>
public sealed record SearchDataListItemsOptions(
    DataListSearchMatchMode MatchMode = DataListSearchMatchMode.Contains,
    string? Locale = null,
    IEnumerable<string>? IncludeLocales = null,
    bool RequireActive = true);

/// <summary>
/// Query for searching data list items by label (locale key).
/// </summary>
public sealed record SearchDataListItemsQuery : IQuery<Result<Paged<DataListItemDto>>>
{
    /// <summary>
    /// The maximum number of items to take.
    /// </summary>
    public const int MaxTake = PagedRequestLimits.MAX_PAGE_SIZE;

    public long DataListId { get; init; }
    public string? Query { get; init; }
    public int Skip { get; init; }
    public int Take { get; init; }
    public DataListSearchMatchMode MatchMode { get; init; }
    public CultureCode? Locale { get; init; }

    /// <summary>
    /// Extra locales to search and project into the labels map. Malformed codes are dropped.
    /// </summary>
    public IReadOnlyList<CultureCode> IncludeLocales { get; init; }

    /// <summary>
    /// When <see langword="true"/> (default), inactive lists are treated as missing.
    /// Management item search sets this to <see langword="false"/>.
    /// </summary>
    public bool RequireActive { get; init; }

    public SearchDataListItemsQuery(
        long dataListId,
        string? query,
        int skip,
        int take,
        SearchDataListItemsOptions? options = null)
    {
        options ??= new SearchDataListItemsOptions();

        Guard.Against.NegativeOrZero(dataListId);
        Guard.Against.Negative(skip);
        Guard.Against.NegativeOrZero(take);
        Guard.Against.EnumOutOfRange(options.MatchMode);

        DataListId = dataListId;
        Query = query?.Trim();
        Skip = skip;
        Take = Math.Min(take, MaxTake);
        MatchMode = options.MatchMode;
        IncludeLocales = TranslationLocaleList.ParseMany(options.IncludeLocales);
        Locale = string.IsNullOrWhiteSpace(options.Locale) ? null : CultureCode.Parse(options.Locale);
        RequireActive = options.RequireActive;
    }
}
