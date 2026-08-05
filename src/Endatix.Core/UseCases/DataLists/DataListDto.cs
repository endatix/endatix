namespace Endatix.Core.UseCases.DataLists;

/// <summary>
/// Data transfer object for a data list.
/// </summary>
public sealed record DataListDto(
    long Id,
    string Name,
    string? Description,
    DateTime CreatedAt,
    DateTime? ModifiedAt,
    bool IsActive,
    int ItemsCount,
    string DefaultLocale,
    IReadOnlyList<string> AvailableLocales,
    IReadOnlyCollection<DataListItemDto> Items);

/// <summary>
/// Data transfer object for a data list item.
/// </summary>
/// <param name="Id">Item id.</param>
/// <param name="Labels">Localized labels including the <c>default</c> key.</param>
/// <param name="Value">Invariant value.</param>
/// <param name="Label">Resolved default label for public/compat consumers (Labels["default"] or Value).</param>
public sealed record DataListItemDto(
    long Id,
    IReadOnlyDictionary<string, string> Labels,
    string Value,
    string Label);
