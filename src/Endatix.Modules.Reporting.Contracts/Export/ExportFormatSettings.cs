namespace Endatix.Modules.Reporting.Contracts.Export;

/// <summary>
/// Tenant-configurable export behavior stored in <c>ExportFormats.SettingsJson</c>.
/// </summary>
public sealed record ExportFormatSettings(
    ColumnAliasProfile AliasProfile = ColumnAliasProfile.Native,
    string Locale = "default",
    IReadOnlyList<string>? ColumnScope = null,
    bool IncludeTestSubmissions = false,
    string KeySeparator = ExportFormatSettings.DefaultKeySeparator)
{
    public const string DefaultKeySeparator = "__";

    /// <summary>
    /// Crunch.io-compatible key separator used until export UI / tenant settings expose <see cref="KeySeparator"/>.
    /// </summary>
    public const string InterimCrunchKeySeparator = "--";

    public static ExportFormatSettings Default { get; } = new();

    /// <summary>
    /// Applies interim hard-coded export defaults for formats not yet configurable from Hub.
    /// </summary>
    public static ExportFormatSettings ForExportFormat(string format, ExportFormatSettings settings)
    {
        if (UsesInterimCrunchKeySeparator(format))
        {
            return settings with { KeySeparator = InterimCrunchKeySeparator };
        }

        return settings;
    }

    public static bool UsesInterimCrunchKeySeparator(string format) =>
        format.Equals("csv", StringComparison.OrdinalIgnoreCase) ||
        format.Equals("codebook-shoji", StringComparison.OrdinalIgnoreCase);

    public ExportFormatSettings MergeRequestOverrides(
        bool? includeTestSubmissions,
        IReadOnlyList<string>? columnScope) =>
        this with
        {
            IncludeTestSubmissions = includeTestSubmissions ?? IncludeTestSubmissions,
            ColumnScope = columnScope is { Count: > 0 } ? columnScope : ColumnScope,
        };
}
