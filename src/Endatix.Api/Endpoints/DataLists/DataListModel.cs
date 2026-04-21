namespace Endatix.Api.Endpoints.DataLists;

public sealed class DataListModel
{
    public long Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public string? Description { get; init; }
    public bool IsActive { get; init; }
    public IReadOnlyCollection<DataListItemModel> Items { get; init; } = [];
}

public sealed class DataListItemModel
{
    public long Id { get; init; }
    public string Label { get; init; } = string.Empty;
    public string Value { get; init; } = string.Empty;
}

public sealed class DataListSearchResultModel
{
    public long DataListId { get; init; }
    public int Total { get; init; }
    public int Skip { get; init; }
    public int Take { get; init; }
    public IReadOnlyCollection<DataListItemModel> Items { get; init; } = [];
}
