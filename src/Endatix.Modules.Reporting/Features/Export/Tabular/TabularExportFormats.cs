namespace Endatix.Modules.Reporting.Features.Export.Tabular;

/// <summary>
/// Export formats served by the reporting read-model tabular data source.
/// </summary>
internal static class TabularExportFormats
{
    internal const string Csv = "csv";
    internal const string Json = "json";

    internal static bool Supports(string format) =>
        format.Equals(Csv, StringComparison.OrdinalIgnoreCase) ||
        format.Equals(Json, StringComparison.OrdinalIgnoreCase);
}
