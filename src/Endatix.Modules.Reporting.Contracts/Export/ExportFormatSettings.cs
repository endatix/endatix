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

    public static string RequireKeySeparator(string keySeparator)
    {
        if (string.IsNullOrWhiteSpace(keySeparator))
        {
            throw new ArgumentException(
                "Export key separator cannot be empty. Configure a non-empty keySeparator in export format settings.",
                nameof(keySeparator));
        }

        return keySeparator;
    }

    public ExportFormatSettings MergeRequestOverrides(
        bool? includeTestSubmissions,
        IReadOnlyList<string>? columnScope,
        string? locale = null) =>
        this with
        {
            IncludeTestSubmissions = includeTestSubmissions ?? IncludeTestSubmissions,
            ColumnScope = columnScope is { Count: > 0 } ? columnScope : ColumnScope,
            Locale = string.IsNullOrWhiteSpace(locale) ? Locale : locale.Trim(),
        };
}
