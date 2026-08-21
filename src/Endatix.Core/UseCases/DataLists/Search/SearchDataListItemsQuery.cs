using Ardalis.GuardClauses;
using Endatix.Core.Common.Translations;
using Endatix.Core.Infrastructure.Messaging;
using Endatix.Core.Infrastructure.Paging;
using Endatix.Core.Infrastructure.Result;

namespace Endatix.Core.UseCases.DataLists.Search;

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
    public bool RequireActive { get; init; }

    public SearchDataListItemsQuery(
        long dataListId,
        string? query,
        int skip,
        int take,
        DataListSearchMatchMode matchMode = DataListSearchMatchMode.Contains,
        string? locale = null,
        IEnumerable<string>? includeLocales = null,
        bool requireActive = true)
    {
        Guard.Against.NegativeOrZero(dataListId);
        Guard.Against.Negative(skip);
        Guard.Against.NegativeOrZero(take);
        Guard.Against.EnumOutOfRange(matchMode);

        DataListId = dataListId;
        Query = query?.Trim();
        Skip = skip;
        Take = Math.Min(take, MaxTake);
        MatchMode = matchMode;
        IncludeLocales = TranslationLocaleList.ParseMany(includeLocales);
        Locale = string.IsNullOrWhiteSpace(locale) ? null : CultureCode.Parse(locale);
        RequireActive = requireActive;
    }
}
