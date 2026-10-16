using System.Globalization;
using System.Text.Json;
using Endatix.Core.Infrastructure.Paging;

namespace Endatix.Modules.Reporting.Domain;

/// <summary>
/// The shape of <see cref="SubmissionView.SettingsJson"/>, version 1:
/// <c>{"v":1,"columns":[{"key","visible","width"}],"pageSize","sort":{"key","direction"}}</c>.
/// </summary>
/// <remarks>
/// <c>columns</c> lists only the columns someone changed, in grid order. <c>visible</c>, <c>width</c>, <c>pageSize</c>
/// and <c>sort</c> are optional. Properties this version does not know are kept as written, so a newer client loses
/// nothing; only the known ones are checked.
/// </remarks>
public static class SubmissionViewSettings
{
    public const int CurrentVersion = 1;

    /// <summary>Caps the stored text. A full reorder of a 4,000-column form fits.</summary>
    public const int MaxJsonLength = 256 * 1024;

    public const int MaxColumnWidth = 4_000;

    public const string AscendingDirection = "asc";

    public const string DescendingDirection = "desc";

    public const string VersionProperty = "v";

    public const string ColumnsProperty = "columns";

    public const string KeyProperty = "key";

    public const string VisibleProperty = "visible";

    public const string WidthProperty = "width";

    public const string PageSizeProperty = "pageSize";

    public const string SortProperty = "sort";

    public const string DirectionProperty = "direction";

    /// <summary>The settings of a form nobody saved a view for: the grid's own defaults apply.</summary>
    public const string DefaultJson = """{"v":1,"columns":[]}""";

    private const int MaxJsonDepth = 8;

    private static readonly JsonDocumentOptions _documentOptions = new() { MaxDepth = MaxJsonDepth };

    /// <summary>Returns why <paramref name="settingsJson"/> cannot be saved, or <see langword="null"/> when it can.</summary>
    public static string? Error(string? settingsJson)
    {
        var textError = TextError(settingsJson);
        if (textError is not null)
        {
            return textError;
        }

        try
        {
            using var document = JsonDocument.Parse(settingsJson, _documentOptions);
            return RootError(document.RootElement);
        }
        catch (JsonException)
        {
            return "Settings must be valid JSON.";
        }
    }

    private static string? TextError(string? settingsJson)
    {
        if (string.IsNullOrWhiteSpace(settingsJson))
        {
            return "Settings are required.";
        }

        return settingsJson.Length > MaxJsonLength
            ? string.Create(CultureInfo.InvariantCulture, $"Settings cannot be longer than {MaxJsonLength} characters.")
            : null;
    }

    private static string? RootError(JsonElement root)
    {
        if (root.ValueKind != JsonValueKind.Object)
        {
            return "Settings must be a JSON object.";
        }

        if (!root.TryGetProperty(VersionProperty, out var version) ||
            version.ValueKind != JsonValueKind.Number ||
            !version.TryGetInt32(out var number) ||
            number != CurrentVersion)
        {
            return $"Settings must have \"{VersionProperty}\": {CurrentVersion}.";
        }

        return ColumnsError(root) ?? PageSizeError(root) ?? SortError(root);
    }

    private static string? ColumnsError(JsonElement root)
    {
        if (!root.TryGetProperty(ColumnsProperty, out var columns) || columns.ValueKind != JsonValueKind.Array)
        {
            return $"Settings must have a \"{ColumnsProperty}\" array.";
        }

        HashSet<string> keys = new(StringComparer.Ordinal);
        foreach (var column in columns.EnumerateArray())
        {
            var error = ColumnError(column, keys);
            if (error is not null)
            {
                return error;
            }
        }

        return null;
    }

    private static string? ColumnError(JsonElement column, HashSet<string> keys)
    {
        if (column.ValueKind != JsonValueKind.Object)
        {
            return "Each column must be a JSON object.";
        }

        var key = NonEmptyString(column, KeyProperty);
        if (key is null)
        {
            return $"Each column must have a non-empty \"{KeyProperty}\".";
        }

        if (!keys.Add(key))
        {
            return $"Column \"{key}\" is listed more than once.";
        }

        return ColumnValuesError(column, key);
    }

    private static string? ColumnValuesError(JsonElement column, string key)
    {
        if (column.TryGetProperty(VisibleProperty, out var visible) &&
            visible.ValueKind is not (JsonValueKind.True or JsonValueKind.False))
        {
            return $"Column \"{key}\" must have a true or false \"{VisibleProperty}\".";
        }

        return column.TryGetProperty(WidthProperty, out var width) && !IsIntegerInRange(width, 1, MaxColumnWidth)
            ? $"Column \"{key}\" must have a \"{WidthProperty}\" from 1 to {MaxColumnWidth}."
            : null;
    }

    private static string? PageSizeError(JsonElement root) =>
        root.TryGetProperty(PageSizeProperty, out var pageSize) &&
        !IsIntegerInRange(pageSize, PagedRequestLimits.MIN_PAGE_SIZE, PagedRequestLimits.MAX_PAGE_SIZE)
            ? $"\"{PageSizeProperty}\" must be from {PagedRequestLimits.MIN_PAGE_SIZE} to {PagedRequestLimits.MAX_PAGE_SIZE}."
            : null;

    private static string? SortError(JsonElement root)
    {
        if (!root.TryGetProperty(SortProperty, out var sort) || sort.ValueKind == JsonValueKind.Null)
        {
            return null;
        }

        var valid = sort.ValueKind == JsonValueKind.Object &&
            NonEmptyString(sort, KeyProperty) is not null &&
            NonEmptyString(sort, DirectionProperty) is AscendingDirection or DescendingDirection;

        return valid
            ? null
            : $"\"{SortProperty}\" must have a \"{KeyProperty}\" and a \"{DirectionProperty}\" of " +
              $"\"{AscendingDirection}\" or \"{DescendingDirection}\".";
    }

    private static string? NonEmptyString(JsonElement element, string property) =>
        element.TryGetProperty(property, out var value) &&
        value.ValueKind == JsonValueKind.String &&
        value.GetString() is { Length: > 0 } text
            ? text
            : null;

    private static bool IsIntegerInRange(JsonElement element, int min, int max) =>
        element.ValueKind == JsonValueKind.Number &&
        element.TryGetInt32(out var value) &&
        value >= min &&
        value <= max;
}
