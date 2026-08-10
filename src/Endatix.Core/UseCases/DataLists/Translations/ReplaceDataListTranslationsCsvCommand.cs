using Ardalis.GuardClauses;
using Endatix.Core.Entities;
using Endatix.Core.Infrastructure.Messaging;
using Endatix.Core.Infrastructure.Result;

namespace Endatix.Core.UseCases.DataLists.Translations;

/// <summary>
/// Command replacing all data list items and their translations from a CSV document.
/// </summary>
public sealed record ReplaceDataListTranslationsCsvCommand : ICommand<Result<DataListDto>>
{
    /// <summary>
    /// Maximum number of data rows accepted in one import.
    /// Bound to <see cref="DataList.MAX_ITEMS"/> — one CSV row maps to one item.
    /// </summary>
    public const int MAX_ROWS = DataList.MAX_ITEMS;

    /// <summary>
    /// The ID of the data list to import into.
    /// </summary>
    public long DataListId { get; init; }

    /// <summary>
    /// The raw CSV document (RFC 4180, header <c>value,default,{locale…}</c>).
    /// </summary>
    public string Csv { get; init; }

    /// <summary>
    /// Cultures to add to AvailableLocales before validating CSV columns (idempotent).
    /// </summary>
    public IReadOnlyList<string> EnsureLocales { get; init; }

    public ReplaceDataListTranslationsCsvCommand(
        long dataListId,
        string csv,
        IEnumerable<string>? ensureLocales = null)
    {
        Guard.Against.NegativeOrZero(dataListId);
        Guard.Against.Null(csv);

        DataListId = dataListId;
        Csv = csv;
        EnsureLocales = ensureLocales is null ? [] : [.. ensureLocales];
    }
}
