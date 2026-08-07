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
    public string? Locale { get; init; }

    public SearchDataListItemsQuery(
        long dataListId,
        string? query,
        int skip,
        int take,
        DataListSearchMatchMode matchMode = DataListSearchMatchMode.Contains,
        string? locale = null)
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
        if (string.IsNullOrWhiteSpace(locale))
        {
            Locale = null;
        }
        else
        {
            var trimmedLocale = locale.Trim();
            Guard.Against.StringTooLong(trimmedLocale, IHasTranslations.MAX_CULTURE_CODE_LENGTH, nameof(locale));
            Locale = trimmedLocale;
        }
    }
}
