namespace Endatix.Modules.Reporting.Contracts.Export;

/// <summary>
/// Tenant-configurable export behavior stored in <c>ExportFormats.SettingsJson</c>.
/// </summary>
public sealed record ExportFormatSettings(
    ColumnAliasProfile AliasProfile = ColumnAliasProfile.Native,
    string Locale = "default",
    IReadOnlyList<string>? ColumnScope = null,
    bool IncludeTestSubmissions = false)
{
    public static ExportFormatSettings Default { get; } = new();

    public ExportFormatSettings MergeRequestOverrides(
        bool? includeTestSubmissions,
        IReadOnlyList<string>? columnScope) =>
        this with
        {
            IncludeTestSubmissions = includeTestSubmissions ?? IncludeTestSubmissions,
            ColumnScope = columnScope ?? ColumnScope,
        };
}
