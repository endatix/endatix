using System.Text.Json;
using System.Text.Json.Serialization;
using Ardalis.GuardClauses;
using Endatix.Core.Abstractions;
using Endatix.Core.Entities;

namespace Endatix.Modules.Audience.Domain;

/// <summary>
/// Up to one chunk of an import's uploaded rows, as raw cells in header order. A chunk is one row however many CSV
/// rows it holds, so a 100,000-row file is about a hundred rows to write and delete.
/// </summary>
public sealed class AudienceImportChunk : BaseEntity, ITenantOwned
{
    /// <summary>Database names of the unique indexes on <see cref="AudienceImportChunk"/>.</summary>
    public static class UniqueConstraints
    {
        /// <summary>One chunk per index of an import.</summary>
        public const string OnePerIndex = "IX_ImportChunks_Index";
    }

    private AudienceImportChunk()
    {
        RowsJson = "[]";
    }

    private AudienceImportChunk(AudienceImport import, ChunkPlace place, IReadOnlyList<StagedRow> rows)
    {
        TenantId = import.TenantId;
        ImportId = import.Id;
        ChunkIndex = place.Index;
        FirstRowNumber = place.FirstRowNumber;
        RowCount = rows.Count;
        RowsJson = JsonSerializer.Serialize(rows);
    }

    /// <summary>The tenant that owns the import.</summary>
    public long TenantId { get; private set; }

    /// <summary>The import the chunk belongs to.</summary>
    public long ImportId { get; private set; }

    /// <summary>The chunk's place in the file, from 0.</summary>
    public int ChunkIndex { get; private set; }

    /// <summary>
    /// The spreadsheet row of the chunk's first data row; the rows after it are numbered in order, except a row that
    /// carries its own number.
    /// </summary>
    public int FirstRowNumber { get; private set; }

    /// <summary>Rows in the chunk.</summary>
    public int RowCount { get; private set; }

    /// <summary>The chunk's rows as JSON: raw cells and the malformed flag, in row order.</summary>
    public string RowsJson { get; private set; }

    /// <summary>A chunk of <paramref name="import"/> at <paramref name="place"/>.</summary>
    public static AudienceImportChunk Create(AudienceImport import, ChunkPlace place, IReadOnlyList<StagedRow> rows)
    {
        Guard.Against.Null(import);
        Guard.Against.Null(place);
        Guard.Against.Negative(place.Index);
        Guard.Against.NegativeOrZero(place.FirstRowNumber);
        Guard.Against.NullOrEmpty(rows);
        return new AudienceImportChunk(import, place, rows);
    }

    /// <summary>The chunk's rows, each with its spreadsheet row number.</summary>
    public IReadOnlyList<NumberedRow> Rows() =>
        (JsonSerializer.Deserialize<List<StagedRow>>(RowsJson) ?? [])
            .Select((row, offset) => new NumberedRow(row.RowNumber ?? FirstRowNumber + offset, row))
            .ToList();
}

/// <summary>Where a chunk sits in its file: its index, and the spreadsheet row of its first row.</summary>
public sealed record ChunkPlace(int Index, int FirstRowNumber);

/// <summary>
/// One uploaded row: its cells in header order, and whether the parser found a stray quote. A file parsed on the
/// server numbers its rows as a spreadsheet shows them, so a blank line or a multi-line cell skips a number; such a
/// row carries <paramref name="RowNumber"/>, and a row without one follows the row before it.
/// </summary>
public sealed record StagedRow(
    IReadOnlyList<string> Cells,
    bool Malformed,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] int? RowNumber = null);

/// <summary>A staged row and the spreadsheet row it came from.</summary>
public sealed record NumberedRow(int RowNumber, StagedRow Row);
