using CsvHelper;
using Endatix.Modules.Audience.Shared;

namespace Endatix.Modules.Audience.Features.Import;

/// <summary>
/// Parses an audience CSV into header + data rows (capped at <see cref="ImportLimits.MaxRows"/>).
/// </summary>
internal static class CsvFileParser
{
    public static CsvFileParseResult Parse(string csvText)
    {
        HashSet<long> malformedLines = [];
        using StringReader reader = new(csvText);
        using CsvReader csv = CreateReader(reader, malformedLines);
        if (!TryReadHeaders(csv, out string[] headers))
        {
            return CsvFileParseResult.Invalid("CSV must include a header row.");
        }

        return ReadRows(csv, headers, malformedLines);
    }

    /// <summary>
    /// A stray quote marks its row instead of failing the file: the row is rejected with its
    /// number, and the rest of the file still imports.
    /// </summary>
    private static CsvReader CreateReader(TextReader reader, HashSet<long> malformedLines) =>
        CsvReader.Create(reader, new CsvReaderOptions
        {
            Separator = ',',
            TrimWhitespace = true,
            TrackLineNumbers = true,
            OnMalformedRecord = record => malformedLines.Add(record.LineNumber),
        });

    private static bool TryReadHeaders(CsvReader csv, out string[] headers)
    {
        headers = csv.Headers.Count == 0 ? [] : [.. csv.Headers];
        return headers.Length > 0;
    }

    private static CsvFileParseResult ReadRows(CsvReader csv, string[] headers, HashSet<long> malformedLines)
    {
        List<CsvDataRow> rows = [];
        long newlinesInsideQuotes = 0;
        while (csv.Read())
        {
            if (rows.Count >= ImportLimits.MaxRows)
            {
                return CsvFileParseResult.Invalid(
                    $"CSV exceeds the maximum of {ImportLimits.MaxRows} data rows.");
            }

            // LineNumber counts every physical line. A quoted cell that spans lines is still one
            // spreadsheet row, so those inner newlines are not part of the row number.
            int row = checked((int)(csv.LineNumber - newlinesInsideQuotes));
            newlinesInsideQuotes += NewlinesInside(csv.RawRecord);
            rows.Add(new CsvDataRow(row, ReadCells(csv, headers), malformedLines.Contains(csv.LineNumber)));
        }

        return CsvFileParseResult.Ok(headers, rows);
    }

    private static int NewlinesInside(ReadOnlySpan<byte> rawRecord)
    {
        int count = 0;
        foreach (byte b in rawRecord)
        {
            if (b == '\n')
            {
                count++;
            }
        }

        return count;
    }

    private static IReadOnlyDictionary<string, string> ReadCells(CsvReader csv, string[] headers)
    {
        Dictionary<string, string> row = new(StringComparer.OrdinalIgnoreCase);
        foreach (string header in headers)
        {
            row[header] = csv.GetString(header);
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
