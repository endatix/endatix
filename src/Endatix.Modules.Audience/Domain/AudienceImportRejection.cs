using System.Text.Json;
using Ardalis.GuardClauses;
using Endatix.Core.Abstractions;
using Endatix.Core.Entities;

namespace Endatix.Modules.Audience.Domain;

/// <summary>
/// One row an import will not write, with the reason and its cells as uploaded. The Check writes these, and the
/// write adds any row a Property changed since the Check now refuses; the write skips every row named here.
/// </summary>
public sealed class AudienceImportRejection : BaseEntity, ITenantOwned
{
    /// <summary>Database names of the unique indexes on <see cref="AudienceImportRejection"/>.</summary>
    public static class UniqueConstraints
    {
        /// <summary>One rejection per spreadsheet row of an import.</summary>
        public const string OnePerRow = "IX_ImportRejections_Row";
    }

    /// <summary>Longest reason a rejection keeps.</summary>
    public const int ReasonMaxLength = 500;

    private AudienceImportRejection()
    {
        Reason = string.Empty;
        CellsJson = "[]";
    }

    private AudienceImportRejection(AudienceImport import, NumberedRow row, string reason)
    {
        TenantId = import.TenantId;
        ImportId = import.Id;
        RowNumber = row.RowNumber;
        Reason = reason.Length > ReasonMaxLength ? reason[..ReasonMaxLength] : reason;
        CellsJson = JsonSerializer.Serialize(row.Row.Cells);
    }

    /// <summary>The tenant that owns the import.</summary>
    public long TenantId { get; private set; }

    /// <summary>The import the row belongs to.</summary>
    public long ImportId { get; private set; }

    /// <summary>The spreadsheet row; row 1 is the header row.</summary>
    public int RowNumber { get; private set; }

    /// <summary>Why the row is not written, for the scripter.</summary>
    public string Reason { get; private set; }

    /// <summary>The row's cells as uploaded, as JSON.</summary>
    public string CellsJson { get; private set; }

    /// <summary>A rejection of <paramref name="row"/> with <paramref name="reason"/>.</summary>
    public static AudienceImportRejection Create(AudienceImport import, NumberedRow row, string reason)
    {
        Guard.Against.Null(import);
        Guard.Against.Null(row);
        Guard.Against.NullOrWhiteSpace(reason);
        return new AudienceImportRejection(import, row, reason);
    }
}
