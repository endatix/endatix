using System.Text;
using Endatix.Core.Common.Translations;

namespace Endatix.Core.UseCases.DataLists.Translations;

/// <summary>
/// A single translations row keyed by the invariant data list item value.
/// </summary>
/// <param name="Value">The invariant item value.</param>
/// <param name="Labels">Labels keyed by label key (<c>default</c> or a culture code). Empty cells are omitted.</param>
public sealed record DataListTranslationRow(string Value, IReadOnlyDictionary<string, string> Labels);

/// <summary>
/// A parsed translations CSV document.
/// </summary>
/// <param name="Columns">Label columns in file order, excluding the leading <c>value</c> column.</param>
/// <param name="Rows">Data rows in file order.</param>
public sealed record DataListTranslationsCsvDocument(
    IReadOnlyList<string> Columns,
    IReadOnlyList<DataListTranslationRow> Rows);

/// <summary>
/// RFC 4180 reader / writer for the SurveyJS-compatible translations CSV
/// (<c>value,default,{locale…}</c> header with one row per item value).
/// </summary>
public static class DataListTranslationsCsv
{
    /// <summary>
    /// Name of the mandatory first column holding the invariant item value.
    /// </summary>
    public const string ValueColumn = "value";

    private const string LineSeparator = "\r\n";

    /// <summary>
    /// Writes the header and rows, quoting fields only when required.
    /// </summary>
    public static string Serialize(IReadOnlyList<string> columns, IEnumerable<DataListTranslationRow> rows)
    {
        ArgumentNullException.ThrowIfNull(columns);
        ArgumentNullException.ThrowIfNull(rows);

        StringBuilder builder = new();
        AppendRecord(builder, [ValueColumn, .. columns]);

        foreach (var row in rows)
        {
            var fields = new string[columns.Count + 1];
            fields[0] = row.Value;
            for (var i = 0; i < columns.Count; i++)
            {
                fields[i + 1] = row.Labels.TryGetValue(columns[i], out var label) ? label : string.Empty;
            }

            AppendRecord(builder, fields);
        }

        return builder.ToString();
    }

    /// <summary>
    /// Parses a translations CSV document. Empty cells are dropped so importers can clear a locale.
    /// </summary>
    /// <exception cref="FormatException">Thrown when the CSV is structurally invalid.</exception>
    public static DataListTranslationsCsvDocument Parse(string csv)
    {
        var records = ReadRecords(csv);
        if (records.Count == 0)
        {
            throw new FormatException("The CSV is empty. A header row starting with 'value' is required.");
        }

        var header = records[0];
        if (header.Length == 0 || !string.Equals(header[0].Trim(), ValueColumn, StringComparison.OrdinalIgnoreCase))
        {
            throw new FormatException($"The first CSV column must be '{ValueColumn}'.");
        }

        string[] columns = [.. header.Skip(1).Select(column => column.Trim())];
        if (columns.Any(string.IsNullOrEmpty))
        {
            throw new FormatException("CSV header columns cannot be empty.");
        }

        List<DataListTranslationRow> rows = new(records.Count - 1);
        for (var i = 1; i < records.Count; i++)
        {
            var record = records[i];
            if (record.Length != header.Length)
            {
                throw new FormatException(
                    $"Row {i + 1} has {record.Length} cells but the header declares {header.Length}.");
            }

            Dictionary<string, string> labels = new(StringComparer.Ordinal);
            for (var column = 0; column < columns.Length; column++)
            {
                var cell = record[column + 1].Trim();
                if (cell.Length > 0)
                {
                    labels[columns[column]] = cell;
                }
            }

            rows.Add(new DataListTranslationRow(record[0].Trim(), labels));
        }

        return new DataListTranslationsCsvDocument(columns, rows);
    }

    /// <summary>
    /// Builds the export column order: <c>default</c> first, then the culture catalog.
    /// </summary>
    public static IReadOnlyList<string> BuildColumns(IHasTranslations catalog)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        return [SurveyJsTranslationKeys.DefaultKey, .. catalog.AvailableCultures];
    }

    private static void AppendRecord(StringBuilder builder, IReadOnlyList<string> fields)
    {
        for (var i = 0; i < fields.Count; i++)
        {
            if (i > 0)
            {
                builder.Append(',');
            }

            builder.Append(EscapeField(fields[i]));
        }

        builder.Append(LineSeparator);
    }

    private static string EscapeField(string field)
    {
        if (field.Length == 0)
        {
            return field;
        }

        var needsQuotes = field.AsSpan().IndexOfAny(",\"\r\n") >= 0
            || char.IsWhiteSpace(field[0])
            || char.IsWhiteSpace(field[^1]);

        return needsQuotes
            ? string.Concat("\"", field.Replace("\"", "\"\"", StringComparison.Ordinal), "\"")
            : field;
    }

    private static IReadOnlyList<string[]> ReadRecords(string csv)
    {
        List<string[]> records = [];
        if (string.IsNullOrEmpty(csv))
        {
            return records;
        }

        var content = csv.AsSpan().TrimStart('\uFEFF');
        List<string> fields = [];
        StringBuilder field = new();
        var inQuotes = false;
        var fieldWasQuoted = false;

        for (var i = 0; i < content.Length; i++)
        {
            var current = content[i];

            if (inQuotes)
            {
                if (current != '"')
                {
                    field.Append(current);
                    continue;
                }

                if (i + 1 < content.Length && content[i + 1] == '"')
                {
                    field.Append('"');
                    i++;
                    continue;
                }

                inQuotes = false;
                continue;
            }

            switch (current)
            {
                case '"':
                    if (field.Length > 0)
                    {
                        throw new FormatException("A quoted CSV field cannot start after unquoted content.");
                    }

                    inQuotes = true;
                    fieldWasQuoted = true;
                    continue;
                case ',':
                    fields.Add(field.ToString());
                    field.Clear();
                    fieldWasQuoted = false;
                    continue;
                case '\r':
                    continue;
                case '\n':
                    CompleteRecord(records, fields, field, fieldWasQuoted);
                    fieldWasQuoted = false;
                    continue;
                default:
                    field.Append(current);
                    continue;
            }
        }

        if (inQuotes)
        {
            throw new FormatException("The CSV ends inside a quoted field.");
        }

        CompleteRecord(records, fields, field, fieldWasQuoted);
        return records;
    }

    private static void CompleteRecord(
        List<string[]> records,
        List<string> fields,
        StringBuilder field,
        bool fieldWasQuoted)
    {
        var isBlankLine = fields.Count == 0 && field.Length == 0 && !fieldWasQuoted;
        if (isBlankLine)
        {
            return;
        }

        fields.Add(field.ToString());
        field.Clear();
        records.Add([.. fields]);
        fields.Clear();
    }
}
