using Endatix.Core.Entities;

namespace Endatix.Core.UseCases.DataLists;

/// <summary>
/// Maps <see cref="DataList"/> aggregates to <see cref="DataListDto"/>.
/// </summary>
public static class DataListDtoMapper
{
    /// <summary>
    /// Maps an entity (with items loaded when needed) to a DTO.
    /// </summary>
    public static DataListDto FromEntity(DataList dataList, bool includeItems = true) =>
        new(
            dataList.Id,
            dataList.Name,
            dataList.Description,
            dataList.CreatedAt,
            dataList.ModifiedAt,
            dataList.IsActive,
            dataList.Items.Count,
            dataList.DefaultLocale,
            dataList.AvailableLocales.ToArray(),
            includeItems
                ? [.. dataList.Items.Select(FromItem)]
                : Array.Empty<DataListItemDto>());

    /// <summary>
    /// Maps a data list item to a DTO.
    /// </summary>
    public static DataListItemDto FromItem(DataListItem item) =>
        new(
            item.Id,
            new Dictionary<string, string>(item.Labels, StringComparer.Ordinal),
            item.Value,
            item.DefaultLabel);
}
