using System.Globalization;
using CsvHelper;
using CsvHelper.Configuration;
using Endatix.Modules.Audience.Shared;

namespace Endatix.Modules.Audience.Features.Import;

/// <summary>
/// Parses an audience CSV into header + data rows (capped at <see cref="ImportLimits.MaxRows"/>).
/// </summary>
internal static class CsvFileParser
{
    public static CsvFileParseResult Parse(string csvText)
    {
        using StringReader reader = new(csvText);
        using CsvReader csv = CreateReader(reader);
        if (!TryReadHeaders(csv, out string[] headers))
        {
            return CsvFileParseResult.Invalid("CSV must include a header row.");
        }

        return ReadRows(csv, headers);
    }

    private static CsvReader CreateReader(TextReader reader) =>
        new(reader, new CsvConfiguration(CultureInfo.InvariantCulture)
        {
            HasHeaderRecord = true,
            TrimOptions = TrimOptions.Trim,
            MissingFieldFound = null,
            BadDataFound = null,
        });

    private static bool TryReadHeaders(CsvReader csv, out string[] headers)
    {
        headers = [];
        if (!csv.Read() || !csv.ReadHeader() || csv.HeaderRecord is null)
        {
            return false;
        }

        headers = csv.HeaderRecord;
        return true;
    }

    private static CsvFileParseResult ReadRows(CsvReader csv, string[] headers)
    {
        List<IReadOnlyDictionary<string, string>> rows = [];
        while (csv.Read())
        {
            if (rows.Count >= ImportLimits.MaxRows)
            {
                return CsvFileParseResult.Invalid(
                    $"CSV exceeds the maximum of {ImportLimits.MaxRows} data rows.");
            }

            rows.Add(ReadRow(csv, headers));
        }

        return CsvFileParseResult.Ok(headers, rows);
    }

    private static IReadOnlyDictionary<string, string> ReadRow(CsvReader csv, string[] headers)
    {
        Dictionary<string, string> row = new(StringComparer.OrdinalIgnoreCase);
        foreach (string header in headers)
        {
            row[header] = csv.GetField(header) ?? string.Empty;
        }

        return row;
    }
}

internal sealed record CsvFileParseResult(
    bool IsSuccess,
    string? Error,
    IReadOnlyList<string> Headers,
    IReadOnlyList<IReadOnlyDictionary<string, string>> Rows)
{
    public static CsvFileParseResult Ok(
        IReadOnlyList<string> headers,
        IReadOnlyList<IReadOnlyDictionary<string, string>> rows) =>
        new(true, null, headers, rows);

    public static CsvFileParseResult Invalid(string error) =>
        new(false, error, [], []);
}
