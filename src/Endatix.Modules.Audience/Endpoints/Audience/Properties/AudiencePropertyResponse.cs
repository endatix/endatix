using Endatix.Modules.Audience.Features.Properties;

namespace Endatix.Modules.Audience.Endpoints.Audience.Properties;

/// <summary>
/// Wire model for an audience property.
/// </summary>
public sealed class AudiencePropertyResponse
{
    public long Id { get; init; }

    public long FormId { get; init; }

    public string VariableName { get; init; } = string.Empty;

    public string Name { get; init; } = string.Empty;

    public string DataType { get; init; } = string.Empty;

    public int SortOrder { get; init; }

    public long? DataListId { get; init; }

    public string? ChoicesJson { get; init; }

    public bool AllowsOther { get; init; }

    internal static AudiencePropertyResponse FromDto(PropertyDto dto) => new()
    {
        Id = dto.Id,
        FormId = dto.FormId,
        VariableName = dto.VariableName,
        Name = dto.Name,
        DataType = dto.DataType,
        SortOrder = dto.SortOrder,
        DataListId = dto.DataListId,
        ChoicesJson = dto.ChoicesJson,
        AllowsOther = dto.AllowsOther,
    };
}
