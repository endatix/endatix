using System.Diagnostics.CodeAnalysis;
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
    /// <summary>The settings version this build reads and writes, stored as <c>"v"</c>.</summary>
    public const int CurrentVersion = 1;

    /// <summary>Caps the stored text. A full reorder of a 4,000-column form fits.</summary>
    public const int MaxJsonLength = 256 * 1024;

    /// <summary>The widest a saved column may be, in pixels.</summary>
    public const int MaxColumnWidth = 4_000;

    /// <summary>The <c>sort.direction</c> of an ascending sort.</summary>
    public const string AscendingDirection = "asc";

    /// <summary>The <c>sort.direction</c> of a descending sort.</summary>
    public const string DescendingDirection = "desc";

    /// <summary>The property holding the settings version.</summary>
    public const string VersionProperty = "v";

    /// <summary>The property holding the changed columns, in grid order.</summary>
    public const string ColumnsProperty = "columns";

    /// <summary>The property naming a column, in a column and in <c>sort</c>.</summary>
    public const string KeyProperty = "key";

    /// <summary>The property telling whether a column shows.</summary>
    public const string VisibleProperty = "visible";

    /// <summary>The property holding a column's width in pixels.</summary>
    public const string WidthProperty = "width";

    /// <summary>The property holding the grid's page size.</summary>
    public const string PageSizeProperty = "pageSize";

    /// <summary>The property holding the grid's sort.</summary>
    public const string SortProperty = "sort";

    /// <summary>The property holding the sort direction.</summary>
    public const string DirectionProperty = "direction";

    /// <summary>The settings of a form nobody saved a view for: the grid's own defaults apply.</summary>
    public const string DefaultJson = """{"v":1,"columns":[]}""";

    private const int MaxJsonDepth = 8;

    private static readonly JsonDocumentOptions _documentOptions = new() { MaxDepth = MaxJsonDepth };

    /// <summary>Returns why <paramref name="settingsJson"/> cannot be saved, or <see langword="null"/> when it can.</summary>
    public static string? Error(string? settingsJson)
    {
        if (TryGetTextError(settingsJson, out var textError))
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

    private static bool TryGetTextError(
        [NotNullWhen(false)] string? settingsJson,
        [NotNullWhen(true)] out string? error)
    {
        error = settingsJson switch
        {
            _ when string.IsNullOrWhiteSpace(settingsJson) => "Settings are required.",
            { Length: > MaxJsonLength } => string.Create(
                CultureInfo.InvariantCulture,
                $"Settings cannot be longer than {MaxJsonLength} characters."),
            _ => null,
        };
        return error is not null;
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
