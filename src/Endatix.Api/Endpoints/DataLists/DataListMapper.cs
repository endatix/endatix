using Endatix.Core.UseCases.DataLists;
using Endatix.Core.UseCases.DataLists.Search;

namespace Endatix.Api.Endpoints.DataLists;

public static class DataListMapper
{
    public static DataListModel Map(DataListDto dto) => new()
    {
        Id = dto.Id,
        Name = dto.Name,
        Description = dto.Description,
        IsActive = dto.IsActive,
        Items = dto.Items.Select(Map).ToArray()
    };

    public static DataListItemModel Map(DataListItemDto dto) => new()
    {
        Id = dto.Id,
        Label = dto.Label,
        Value = dto.Value
    };

    public static DataListSearchResultModel Map(SearchDataListItemsDto dto) => new()
    {
        DataListId = dto.DataListId,
        Total = dto.Total,
        Skip = dto.Skip,
        Take = dto.Take,
        Items = dto.Items.Select(Map).ToArray()
    };

    public static DataListPublicChoiceModel MapPublic(DataListItemDto dto) => new()
    {
        Label = dto.Label,
        Value = dto.Value
    };

    public static DataListPublicSearchResultModel MapPublic(SearchDataListItemsDto dto) => new()
    {
        DataListId = dto.DataListId,
        Total = dto.Total,
        Skip = dto.Skip,
        Take = dto.Take,
        Items = dto.Items.Select(MapPublic).ToArray()
    };

    public static DataListPublicChoiceModel MapPublic(DataListChoiceDisplayValueDto dto) => new()
    {
        Label = dto.Label,
        Value = dto.Value
    };
}
