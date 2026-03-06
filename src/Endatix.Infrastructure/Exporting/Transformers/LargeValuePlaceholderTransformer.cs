using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Endatix.Core.Abstractions.Exporting;
using Endatix.Core.Entities;

namespace Endatix.Infrastructure.Exporting.Transformers;

/// <summary>
/// Replaces data URIs (base64) and very long string values with placeholders to keep export payload size within client/transport limits.
/// </summary>
public sealed class LargeValuePlaceholderTransformer : IValueTransformer
{
    /// <summary>
    /// Maximum character length for string values; longer values are replaced with a placeholder.
    /// </summary>
    public const int DefaultMaxValueLength = 10_000;

    private static readonly TimeSpan _regexMatchTimeout = TimeSpan.FromMilliseconds(100);
    private static readonly Regex _dataUriPattern = new(@"^data:[^;]+;base64,", RegexOptions.Compiled, _regexMatchTimeout);

    private readonly int _maxValueLength;

    public LargeValuePlaceholderTransformer() : this(DefaultMaxValueLength)
    {
    }

    public LargeValuePlaceholderTransformer(int maxValueLength)
    {
        _maxValueLength = maxValueLength > 0 ? maxValueLength : DefaultMaxValueLength;
    }

    public JsonNode? Transform<T>(JsonNode? node, TransformationContext<T> context)
    {
        if (node is null)
        {
            return node;
        }

        if (context.Row is not SubmissionExportRow)
        {
            return node;
        }

        switch (node)
        {
            case JsonArray array:
                ProcessArray(array);
                break;
            case JsonObject obj:
                ProcessObject(obj);
                break;
            case JsonValue value:
                return TransformValue(value);
        }

        return node;
    }

    private void ProcessArray(JsonArray array)
    {
        for (var i = 0; i < array.Count; i++)
        {
            switch (array[i])
            {
                case JsonArray subArray:
                    ProcessArray(subArray);
                    break;
                case JsonObject obj:
                    ProcessObject(obj);
                    break;
                case JsonValue val:
                    var transformed = TransformValue(val);
                    if (transformed is not null)
                    {
                        array[i] = transformed;
                    }
                    break;
            }
        }
    }

    private void ProcessObject(JsonObject obj)
    {
        foreach (var prop in obj.ToList())
        {
            var value = prop.Value;
            if (value is null)
            {
                continue;
            }

            switch (value)
            {
                case JsonArray arr:
                    ProcessArray(arr);
                    break;
                case JsonObject childObj:
                    ProcessObject(childObj);
                    break;
                case JsonValue jsonVal:
                    var transformed = TransformValue(jsonVal);
                    if (transformed is not null)
                    {
                        obj[prop.Key] = transformed;
                    }
                    break;
            }
        }
    }

    private JsonNode? TransformValue(JsonValue value)
    {
        if (value.TryGetValue<string>(out var s))
        {
            if (_dataUriPattern.IsMatch(s))
            {
                return JsonValue.Create("[file]");
            }

            if (s.Length > _maxValueLength)
            {
                return JsonValue.Create("[truncated]");
            }
        }

        return value;
    }
}
