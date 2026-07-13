using System.Text.Json;
using System.Text.Json.Serialization;
using Endatix.Modules.Reporting.Contracts.Export;
using Microsoft.Extensions.Logging;

namespace Endatix.Modules.Reporting.Features.Export;

/// <summary>
/// Parses persisted <c>ExportFormats.SettingsJson</c>.
/// </summary>
internal sealed partial class ExportFormatSettingsParser(ILogger<ExportFormatSettingsParser> logger)
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
    };

    internal ExportFormatSettings Parse(string? settingsJson)
    {
        if (string.IsNullOrWhiteSpace(settingsJson))
        {
            return ExportFormatSettings.Default;
        }

        try
        {
            ExportFormatSettings? settings = JsonSerializer.Deserialize<ExportFormatSettings>(settingsJson, SerializerOptions);
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
        IReadOnlyList<string>? columnScope) =>
        Parse(settingsJson).MergeRequestOverrides(includeTestSubmissions, columnScope);

    [LoggerMessage(
        Level = LogLevel.Debug,
        Message = "Failed to parse export format settings JSON: {SettingsJson}")]
    private partial void LogParseFailure(JsonException ex, string settingsJson);
}
