using Endatix.Core.Entities;

namespace Endatix.Infrastructure.Exporting;

/// <summary>
/// XLSX-only: store long IDs as text so Excel does not coerce them to numbers. CSV stays raw.
/// </summary>
internal static class ExcelIdCell
{
    public const int MinDigitLengthForAnswerIds = 16;

    private static readonly HashSet<string> SystemIdColumns = new(StringComparer.Ordinal)
    {
        SubmissionExportRow.SystemColumns.Id,
        SubmissionExportRow.SystemColumns.FormId,
        SubmissionExportRow.SystemColumns.SubmitterId,
        SubmissionExportRow.SystemColumns.SubmitterDisplayId,
    };

    /// <summary>
    /// System ID columns always; answers only when they are long enough to lose precision in
    /// Excel's 15-significant-digit numeric type.
    /// </summary>
    public static bool ShouldWriteAsText(string columnName, string value) =>
        SystemIdColumns.Contains(columnName) ||
        (value.Length >= MinDigitLengthForAnswerIds && value.All(char.IsAsciiDigit));
}
