using System.Text.RegularExpressions;
using Endatix.Modules.Reporting.Features.SubmissionGrid;

namespace Endatix.Modules.Reporting.Features.SearchCopy;

/// <summary>
/// How a text answer is written into the search copy: text lowercased, so a contains filter ignores case whatever the
/// database collation; dates as <c>yyyy-MM-dd</c> and date-times as <c>yyyy-MM-ddTHH:mm:ss</c>, so a range compares
/// them as text.
/// </summary>
/// <remarks>
/// Dates are read by their ISO shape, not parsed, so the SQL rebuild (<see cref="SearchCopyRebuildSql"/>) writes the
/// same values from the stored answers. A time zone is ignored: answers carry the clock time as entered. A value of
/// another shape is written as entered, and never matches a range.
/// </remarks>
internal static partial class SearchValues
{
    public static string Normalize(string value, string filterType) =>
        filterType switch
        {
            SubmissionGridFilterTypes.Date => NormalizeDate(value),
            SubmissionGridFilterTypes.DateTime => NormalizeDateTime(value),
            _ => value.ToLowerInvariant(),
        };

    private static string NormalizeDate(string value)
    {
        var match = DatePrefix().Match(value);
        return match.Success ? match.Groups["date"].Value : value;
    }

    private static string NormalizeDateTime(string value)
    {
        var withTime = DateTimePrefix().Match(value);
        if (withTime.Success)
        {
            var seconds = withTime.Groups["seconds"].Success ? withTime.Groups["seconds"].Value : ":00";
            return $"{withTime.Groups["date"].Value}T{withTime.Groups["time"].Value}{seconds}";
        }

        return DateOnly().IsMatch(value) ? $"{value}T00:00:00" : value;
    }

    [GeneratedRegex("^(?<date>[0-9]{4}-[0-9]{2}-[0-9]{2})", RegexOptions.CultureInvariant)]
    private static partial Regex DatePrefix();

    [GeneratedRegex("^(?<date>[0-9]{4}-[0-9]{2}-[0-9]{2})[Tt ](?<time>[0-9]{2}:[0-9]{2})(?<seconds>:[0-9]{2})?", RegexOptions.CultureInvariant)]
    private static partial Regex DateTimePrefix();

    [GeneratedRegex(@"^[0-9]{4}-[0-9]{2}-[0-9]{2}\z", RegexOptions.CultureInvariant)]
    private static partial Regex DateOnly();
}
