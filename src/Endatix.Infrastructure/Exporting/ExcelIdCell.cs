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

    public static bool ShouldWriteAsText(string columnName, string? formattedValue)
    {
        if (string.IsNullOrEmpty(formattedValue) ||
            string.Equals(formattedValue, "N/A", StringComparison.Ordinal))
        {
            return false;
        }

        if (SystemIdColumns.Contains(columnName))
        {
            return true;
        }

        return IsLongDigitString(formattedValue);
    }

    private static bool IsLongDigitString(string text)
    {
        if (text.Length < MinDigitLengthForAnswerIds)
        {
            return false;
        }

        foreach (var c in text)
        {
            if (!char.IsAsciiDigit(c))
            {
                return false;
            }
        }

        return true;
    }
}
