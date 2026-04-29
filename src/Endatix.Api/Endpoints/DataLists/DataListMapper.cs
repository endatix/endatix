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
    /// <param name="dto">The data list DTO.</param>
    /// <returns>The data list model.</returns>
    public static DataListModel Map(DataListDto dto) => new()
    {
        Id = dto.Id,
        Name = dto.Name,
        Description = dto.Description,
        IsActive = dto.IsActive,
        CreatedAt = dto.CreatedAt,
        ModifiedAt = dto.ModifiedAt,
        ItemsCount = dto.ItemsCount
    };

    /// <summary>
    /// Maps a data list DTO to a data list details model.
    /// </summary>
    /// <param name="dto">The data list DTO.</param>
    /// <returns>The data list details model.</returns>
    public static DataListDetailsModel MapDetails(DataListDto dto) => new()
    {
        Id = dto.Id,
        Name = dto.Name,
        Description = dto.Description,
        IsActive = dto.IsActive,
        CreatedAt = dto.CreatedAt,
        ModifiedAt = dto.ModifiedAt,
        ItemsCount = dto.ItemsCount,
        Items = [.. dto.Items.Select(Map)]
    };

    /// <summary>
    /// Maps a data list item to a data list item model.
    /// </summary>
    /// <param name="dto">The data list item dto.</param>
    /// <returns>The data list item model.</returns>
    public static DataListItemModel Map(DataListItemDto dto) => new()
    {
        Id = dto.Id,
        Label = dto.Label,
        Value = dto.Value
    };

    /// <summary>
    /// Maps a data list choice to a public choice model.
    /// </summary>
    /// <param name="dto">The data list choice dto.</param>
    /// <returns>The public choice model.</returns>
    public static DataListPublicChoiceModel MapPublic(DataListItemDto dto) => new()
    {
        Label = dto.Label,
        Value = dto.Value
    };

    /// <summary>
    /// Maps a data list choice display value to a public choice model.
    /// </summary>
    /// <param name="dto">The data list choice display value dto.</param>
    /// <returns>The public choice model.</returns>
    public static DataListPublicChoiceModel MapPublic(DataListChoiceDisplayValueDto dto) => new()
    {
        Label = dto.Label,
        Value = dto.Value
    };
}
