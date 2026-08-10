using Endatix.Core.UseCases.DataLists;
using Endatix.Core.UseCases.DataLists.Search;

namespace Endatix.Api.Endpoints.DataLists;

/// <summary>
/// Maps data list DTOs to models.
/// </summary>
public static class DataListMapper
{
    /// <summary>
    /// Maps a data list DTO to a data list model.
    /// </summary>
    public static DataListModel Map(DataListDto dto) => new()
    {
        Id = dto.Id,
        Name = dto.Name,
        Description = dto.Description,
        IsActive = dto.IsActive,
        CreatedAt = dto.CreatedAt,
        ModifiedAt = dto.ModifiedAt,
        ItemsCount = dto.ItemsCount,
        DefaultLocale = dto.DefaultLocale,
        AvailableLocales = dto.AvailableLocales
    };

    /// <summary>
    /// Maps a data list item to a data list item model.
    /// </summary>
    public static DataListItemModel Map(DataListItemDto dto) => new()
    {
        Id = dto.Id,
        Labels = dto.Labels,
        Label = dto.Label,
        Value = dto.Value
    };

    /// <summary>
    /// Maps a data list DTO to a data list details model.
    /// </summary>
    public static DataListDetailsModel MapDetails(DataListDto dto) => new()
    {
        Id = dto.Id,
        Name = dto.Name,
        Description = dto.Description,
        IsActive = dto.IsActive,
        CreatedAt = dto.CreatedAt,
        ModifiedAt = dto.ModifiedAt,
        ItemsCount = dto.ItemsCount,
        DefaultLocale = dto.DefaultLocale,
        AvailableLocales = dto.AvailableLocales,
        Items = [.. dto.Items.Select(Map)]
    };

    /// <summary>
    /// Maps a searched data list choice to a public choice model. Labels are already projected to the requested locales.
    /// </summary>
    public static DataListPublicChoiceModel MapPublic(DataListItemDto dto) => new()
    {
        Value = dto.Value,
        Labels = dto.Labels
    };

    /// <summary>
    /// Maps a data list choice display value to a public choice model.
    /// </summary>
    public static DataListPublicChoiceModel MapPublic(DataListChoiceDisplayValueDto dto) => new()
    {
        Value = dto.Value,
        Labels = dto.Labels
    };
}
