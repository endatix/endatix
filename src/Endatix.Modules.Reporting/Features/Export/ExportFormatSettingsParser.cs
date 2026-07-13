using System.Text.Json;
using System.Text.Json.Serialization;
using Endatix.Modules.Reporting.Contracts.Export;

namespace Endatix.Modules.Reporting.Features.Export;

/// <summary>
/// Parses persisted <c>ExportFormats.SettingsJson</c>.
/// </summary>
internal static class ExportFormatSettingsParser
{
    private static readonly JsonSerializerOptions _serializerOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
    };

    internal static ExportFormatSettings Parse(string? settingsJson)
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
        catch (JsonException)
        {
            return ExportFormatSettings.Default;
        }
    }

    internal static ExportFormatSettings Resolve(
        string? settingsJson,
        bool? includeTestSubmissions,
        IReadOnlyList<string>? columnScope) =>
        Parse(settingsJson).MergeRequestOverrides(includeTestSubmissions, columnScope);
}
