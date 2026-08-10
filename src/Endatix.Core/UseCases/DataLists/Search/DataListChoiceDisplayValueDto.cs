namespace Endatix.Core.UseCases.DataLists.Search;

/// <summary>
/// Data list choice display value DTO (<c>value</c> + projected <c>labels</c> map).
/// </summary>
/// <param name="Value">The invariant item value.</param>
/// <param name="Labels">Localized labels for the requested locales, always including <c>default</c>.</param>
public sealed record DataListChoiceDisplayValueDto(
    string Value,
    IReadOnlyDictionary<string, string> Labels);
