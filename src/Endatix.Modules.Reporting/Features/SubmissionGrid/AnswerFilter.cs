using System.Globalization;

namespace Endatix.Modules.Reporting.Features.SubmissionGrid;

/// <summary>
/// One answer filter of the grid's submissions list: a search key of the form, an operator, and a value.
/// </summary>
/// <param name="Key">The flattened column key.</param>
/// <param name="Operator">One of <see cref="AnswerFilterOperators"/>.</param>
/// <param name="Value">The text to find, or a calendar day as <c>yyyy-MM-dd</c>.</param>
public sealed record AnswerFilter(string Key, string Operator, string Value);

/// <summary>The operators an answer filter can use.</summary>
public static class AnswerFilterOperators
{
    /// <summary>The answer contains the value, ignoring case. For <c>text</c> columns.</summary>
    public const string Contains = "contains";

    /// <summary>The answer is on or after the day. For <c>date</c> and <c>datetime</c> columns.</summary>
    public const string From = "from";

    /// <summary>The answer is on or before the day. For <c>date</c> and <c>datetime</c> columns.</summary>
    public const string To = "to";

    /// <summary>Whether a column of <paramref name="filterType"/> supports <paramref name="operator"/>.</summary>
    public static bool Supports(string filterType, string @operator) =>
        filterType switch
        {
            SubmissionGridFilterTypes.Text => @operator is Contains,
            SubmissionGridFilterTypes.Date or SubmissionGridFilterTypes.DateTime => @operator is From or To,
            _ => false,
        };
}

/// <summary>
/// The <c>answer</c> parameter of the grid's submissions list: <c>{operator}:{key}:{value}</c>, repeated for each
/// filter and AND-combined.
/// </summary>
/// <remarks>
/// A column key can contain any character, <c>:</c> included, so the key is percent-encoded
/// (<c>encodeURIComponent</c>). The value is everything after the second <c>:</c> and is taken as written.
/// </remarks>
public static class AnswerFilterExpressions
{
    /// <summary>The most answer filters one request can use.</summary>
    public const int MaxFilters = 10;

    /// <summary>The longest text a contains filter accepts.</summary>
    public const int MaxTextLength = 200;

    private const string DayFormat = "yyyy-MM-dd";

    /// <summary>
    /// The filters in <paramref name="expressions"/>, or <see langword="null"/> when one is malformed. Blank
    /// expressions are skipped.
    /// </summary>
    public static IReadOnlyList<AnswerFilter>? TryParse(IEnumerable<string>? expressions)
    {
        List<AnswerFilter> filters = [];
        foreach (var expression in expressions ?? [])
        {
            if (string.IsNullOrWhiteSpace(expression))
            {
                continue;
            }

            if (TryParseOne(expression) is not { } filter)
            {
                return null;
            }

            filters.Add(filter);
        }

        return filters;
    }

    /// <summary>Whether <paramref name="day"/> is a calendar day written as <c>yyyy-MM-dd</c>.</summary>
    public static bool IsDay(string day) =>
        DateOnly.TryParseExact(day, DayFormat, CultureInfo.InvariantCulture, DateTimeStyles.None, out _);

    /// <summary>The day after <paramref name="day"/>, as <c>yyyy-MM-dd</c>.</summary>
    internal static string NextDay(string day) =>
        DateOnly.ParseExact(day, DayFormat, CultureInfo.InvariantCulture)
            .AddDays(1)
            .ToString(DayFormat, CultureInfo.InvariantCulture);

    private static AnswerFilter? TryParseOne(string expression)
    {
        var parts = expression.Split(':', 3);
        if (parts.Length != 3 || !TryDecodeKey(parts[1], out var key))
        {
            return null;
        }

        var @operator = parts[0].Trim().ToLowerInvariant();
        var value = @operator is AnswerFilterOperators.Contains ? parts[2].Trim() : parts[2];
        return IsValidValue(@operator, value) ? new AnswerFilter(key, @operator, value) : null;
    }

    private static bool IsValidValue(string @operator, string value) =>
        @operator switch
        {
            AnswerFilterOperators.Contains => value.Length is > 0 and <= MaxTextLength,
            AnswerFilterOperators.From or AnswerFilterOperators.To => IsDay(value),
            _ => false,
        };

    // An invalid escape is kept as written, so it names no key rather than failing the request.
    private static bool TryDecodeKey(string encodedKey, out string key)
    {
        key = Uri.UnescapeDataString(encodedKey);
        return key.Length is > 0 and <= SubmissionGridColumnKeys.MaxKeyLength;
    }
}
