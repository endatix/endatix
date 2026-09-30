using Endatix.Core.Common;
using Endatix.Modules.Audience.Domain;

namespace Endatix.Modules.Audience.Features.Import;

/// <summary>
/// Turns parsed CSV rows into accepted rows and rejected rows. Pure: touches no database.
/// </summary>
internal static class ImportRowReader
{
    /// <summary>Row 1 is the header, so the first data row is row 2 in the file.</summary>
    private const int FirstDataRowNumber = 2;

    public static ImportRows Read(
        IReadOnlyList<IReadOnlyDictionary<string, string>> rows,
        CsvColumnMap map,
        string identifierKind)
    {
        RowSorter sorter = new(map, identifierKind);
        for (int index = 0; index < rows.Count; index++)
        {
            sorter.Add(rows[index], index + FirstDataRowNumber);
        }

        return new ImportRows(sorter.Accepted, sorter.Rejected);
    }

    private sealed class RowSorter(CsvColumnMap map, string identifierKind)
    {
        private readonly HashSet<string> _seen = new(StringComparer.Ordinal);

        public List<ImportRow> Accepted { get; } = [];

        public List<ImportRejectionDto> Rejected { get; } = [];

        public void Add(IReadOnlyDictionary<string, string> row, int rowNumber)
        {
            string identifier = Member.Normalize(
                row.GetValueOrDefault(map.IdentifierColumn) ?? string.Empty, identifierKind);
            string? error = IdentifierError(identifier);
            if (error is not null)
            {
                Rejected.Add(new ImportRejectionDto(rowNumber, error));
                return;
            }

            Accepted.Add(new ImportRow(identifier, CsvColumnMapper.ReadValues(map, row)));
        }

        private string? IdentifierError(string identifier)
        {
            if (identifier.Length == 0)
            {
                return "Identifier is required.";
            }

            if (identifier.Length > DataSchemaConstants.MAX_EMAIL_LENGTH)
            {
                return $"Identifier is longer than {DataSchemaConstants.MAX_EMAIL_LENGTH} characters.";
            }

            return _seen.Add(identifier) ? null : "Identifier appears more than once in the file.";
        }
    }
}

/// <summary>
/// One accepted data row: normalized identifier and property values keyed by property id.
/// </summary>
internal sealed record ImportRow(string Identifier, IReadOnlyDictionary<long, string> Values);

/// <summary>
/// Rows to write, and every row refused with its reason.
/// </summary>
internal sealed record ImportRows(
    IReadOnlyList<ImportRow> Accepted,
    IReadOnlyList<ImportRejectionDto> Rejected);
