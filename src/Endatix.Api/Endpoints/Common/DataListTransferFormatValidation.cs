using FluentValidation;

namespace Endatix.Api.Endpoints.Common;

/// <summary>
/// Shared FluentValidation rules for data list import/export <c>format</c> (<c>csv</c> | <c>json</c>).
/// </summary>
public static class DataListTransferFormatValidation
{
    /// <summary>SurveyJS translations CSV.</summary>
    public const string FormatCsv = "csv";

    /// <summary>JSON array / items payload.</summary>
    public const string FormatJson = "json";

    /// <summary>Shared invalid-format message (csv listed first for import and export).</summary>
    public const string InvalidFormatMessage = "Format must be 'csv' or 'json'.";

    /// <summary>
    /// Trims and lowercases <paramref name="format"/>; blank values become <paramref name="defaultFormat"/>.
    /// </summary>
    public static string Normalize(string? format, string defaultFormat) =>
        string.IsNullOrWhiteSpace(format)
            ? defaultFormat
            : format.Trim().ToLowerInvariant();

    /// <summary>
    /// Accepts blank (caller default), <see cref="FormatCsv"/>, or <see cref="FormatJson"/>.
    /// </summary>
    public static IRuleBuilderOptions<T, string?> IsDataListFileFormat<T>(
        this IRuleBuilder<T, string?> ruleBuilder,
        string defaultFormat) =>
        ruleBuilder
            .Must(format => IsKnownFormat(Normalize(format, defaultFormat)))
            .WithMessage(InvalidFormatMessage);

    private static bool IsKnownFormat(string normalized) =>
        normalized is FormatCsv or FormatJson;
}
