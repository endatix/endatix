using System.Text.Json;
using Endatix.Modules.Reporting.Contracts.Export;
using Microsoft.Extensions.Logging;

namespace Endatix.Modules.Reporting.Features.Export;

/// <summary>
/// Parses persisted <c>ExportFormats.SettingsJson</c>.
/// </summary>
internal sealed partial class ExportFormatSettingsParser(ILogger<ExportFormatSettingsParser> logger)
{
    private static readonly JsonSerializerOptions _serializerOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new ColumnAliasProfileJsonConverter() },
    };

    internal ExportFormatSettings Parse(string? settingsJson)
    {
        if (string.IsNullOrWhiteSpace(settingsJson))
        {
            return ExportFormatSettings.Default;
        }

        try
        {
            var settings = JsonSerializer.Deserialize<ExportFormatSettings>(settingsJson, _serializerOptions);
            return settings ?? ExportFormatSettings.Default;
        }
        catch (JsonException ex)
        {
            LogParseFailure(ex, settingsJson);
            return ExportFormatSettings.Default;
        }
    }

    internal ExportFormatSettings Resolve(
        string? settingsJson,
        bool? includeTestSubmissions,
        IReadOnlyList<string>? columnScope,
        string? locale = null) =>
        Parse(settingsJson).MergeRequestOverrides(includeTestSubmissions, columnScope, locale);

    [LoggerMessage(
        Level = LogLevel.Debug,
        Message = "Failed to parse export format settings JSON: {SettingsJson}")]
    private partial void LogParseFailure(JsonException ex, string settingsJson);
}
