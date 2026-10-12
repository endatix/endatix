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
        HashSet<int> malformedRows = [];
        using StringReader reader = new(csvText);
        using CsvReader csv = CreateReader(reader, malformedRows);
        if (!TryReadHeaders(csv, out string[] headers))
        {
            return CsvFileParseResult.Invalid("CSV must include a header row.");
        }

        return ReadRows(csv, headers, malformedRows);
    }

    /// <summary>
    /// A stray quote marks its row instead of failing the file: the row is rejected with its
    /// number, and the rest of the file still imports.
    /// </summary>
    private static CsvReader CreateReader(TextReader reader, HashSet<int> malformedRows) =>
        new(reader, new CsvConfiguration(CultureInfo.InvariantCulture)
        {
            HasHeaderRecord = true,
            TrimOptions = TrimOptions.Trim,
            MissingFieldFound = null,
            BadDataFound = args => malformedRows.Add(args.Context.Parser!.Row),
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

    private static CsvFileParseResult ReadRows(CsvReader csv, string[] headers, HashSet<int> malformedRows)
    {
        List<CsvDataRow> rows = [];
        while (csv.Read())
        {
            if (rows.Count >= ImportLimits.MaxRows)
            {
                return CsvFileParseResult.Invalid(
                    $"CSV exceeds the maximum of {ImportLimits.MaxRows} data rows.");
            }

            int row = csv.Parser.Row;
            rows.Add(new CsvDataRow(row, ReadCells(csv, headers), malformedRows.Contains(row)));
        }

        return CsvFileParseResult.Ok(headers, rows);
    }

    private static IReadOnlyDictionary<string, string> ReadCells(CsvReader csv, string[] headers)
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
    IReadOnlyList<CsvDataRow> Rows)
{
    public static CsvFileParseResult Ok(
        IReadOnlyList<string> headers,
        IReadOnlyList<CsvDataRow> rows) =>
        new(true, null, headers, rows);

    public static CsvFileParseResult Invalid(string error) =>
        new(false, error, [], []);
}

/// <summary>
/// One CSV data record and the spreadsheet row it starts on. Blank lines count as rows and a
/// quoted cell spanning several lines stays one row, as a spreadsheet shows the file.
/// <paramref name="Malformed"/> marks a row with a stray quote, whose cells cannot be trusted.
/// </summary>
internal sealed record CsvDataRow(
    int RowNumber,
    IReadOnlyDictionary<string, string> Cells,
    bool Malformed = false);
