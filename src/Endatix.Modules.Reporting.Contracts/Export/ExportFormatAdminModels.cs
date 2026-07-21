namespace Endatix.Modules.Reporting.Contracts.Export;

/// <summary>
/// Tenant export format for admin APIs and Hub settings UI.
/// </summary>
public sealed record ExportFormatDto(
    long Id,
    string Name,
    ExportTarget ExportTarget,
    ExportDeliveryFormat DeliveryFormat,
    ExportProfile Profile,
    string WireKey,
    string Label,
    string? Description,
    ExportFormatSettings Settings,
    DateTime CreatedAt,
    DateTime? ModifiedAt,
    IReadOnlyList<string> AllowedFilters);

/// <summary>
/// Links a tenant or survey-type scope to an export format.
/// </summary>
public sealed record ExportMappingDto(
    long Id,
    long ExportFormatId,
    long? SurveyTypeId,
    bool IsDefault,
    ExportFormatDto? ExportFormat);

/// <summary>
/// Upsert payload for tenant export mappings.
/// </summary>
public sealed record UpsertExportMappingRequest(
    long ExportFormatId,
    long? SurveyTypeId,
    bool IsDefault);

/// <summary>
/// Settings payload for create/update export format requests.
/// </summary>
public sealed record ExportFormatSettingsInput(
    ColumnAliasProfile AliasProfile = ColumnAliasProfile.Native,
    string Locale = "default",
    IReadOnlyList<string>? ColumnScope = null,
    bool IncludeTestSubmissions = false,
    string KeySeparator = ExportFormatSettings.DefaultKeySeparator)
{
    public ExportFormatSettings ToSettings() =>
        new(AliasProfile, Locale, ColumnScope, IncludeTestSubmissions, KeySeparator);
}
