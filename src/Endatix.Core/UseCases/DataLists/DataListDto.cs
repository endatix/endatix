namespace Endatix.Core.UseCases.DataLists;

/// <summary>
/// Data transfer object for a data list.
/// </summary>
public sealed record DataListDto(
    long Id,
    string Name,
    string? Description,
    bool IsActive,
    IReadOnlyCollection<DataListItemDto> Items);

/// <summary>
/// Data transfer object for a data list item.
/// </summary>
public sealed record DataListItemDto(
    long Id,
    string Label,
    string Value);
