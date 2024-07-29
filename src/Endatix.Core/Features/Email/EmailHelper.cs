using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using Endatix.Core.Entities;

namespace Endatix.Core;

/// <summary>
/// Static class with email helpers including text generation and JSON parsing
/// </summary>
public static class EmailHelper
{
    /// <summary>
    /// Generates formatted HTML message given a submission
    /// </summary>
    /// <param name="submission"></param>
    /// <returns>Formatted HTML message</returns>
    public static string ToFormattedMessage(Submission submission)
    {
        if (submission == null)
        {
            return string.Empty;
        }

        var sb = new StringBuilder();

        sb.AppendLine($"Id: {submission.Id}");
        sb.AppendLine($"<br>CreatedAt: {submission.CreatedAt}");
        sb.AppendLine($"<br>Current Page: {submission.CurrentPage}");
        sb.AppendLine($"<br>IsComplete: {submission.IsComplete}");
        sb.AppendLine($"<br>FormDefinitionId: {submission.FormDefinitionId}");

        sb.AppendLine($"<br>Metadata: {submission.Metadata}");
        sb.AppendLine("<br>");

        var submissionData = ParseJsonToDictionary(submission.JsonData);
        if (submissionData.Count > 0)
        {
            sb.AppendLine("<h2>Submission Data:</h2>");
            foreach (var (key, value) in submissionData)
            {
                sb.AppendLine($"{key} : {value}<br>");
            }
        }

        return sb.ToString();
    }

    /// <summary>
    /// Converts form submission JSON to dictionary
    /// </summary>
    /// <param name="jsonString"></param>
    /// <returns>Case insensitive dictionary</returns>
    public static Dictionary<string, string> ParseJsonToDictionary(string jsonString)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        using (JsonDocument document = JsonDocument.Parse(jsonString))
        {
            JsonElement root = document.RootElement;
            foreach (JsonProperty property in root.EnumerateObject())
            {
                var propValue = GetValue(property.Value);
                var propValueAsString = GetStringValue(propValue);

                result[property.Name] = propValueAsString;
            }
        }

        return result;
    }

    private static object GetValue(JsonElement element)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                var nestedDict = new Dictionary<string, object>();
                foreach (JsonProperty subProperty in element.EnumerateObject())
                {
                    nestedDict[subProperty.Name] = GetValue(subProperty.Value);
                }
                return nestedDict;

            case JsonValueKind.Array:
                var list = new List<object>();
                foreach (JsonElement item in element.EnumerateArray())
                {
                    list.Add(GetValue(item));
                }
                return list;

            case JsonValueKind.String:
                return element.GetString();

            case JsonValueKind.Number:
                if (element.TryGetInt32(out int intValue))
                {
                    return intValue;
                }
                return element.GetDouble();

            case JsonValueKind.True:
                return true;

            case JsonValueKind.False:
                return false;

            case JsonValueKind.Null:
                return null;

            default:
                throw new InvalidOperationException($"Unsupported JsonValueKind: {element.ValueKind}");
        }
    }

    private static string GetStringValue(object value)
    {
        if (IsGenericCollection(value))
        {
            Type objType = value.GetType();
            Type elementType = objType.GetGenericArguments().First();

            if (value is IEnumerable enumerable)
            {
                var elements = enumerable.Cast<object>().Select(e => e.ToString());
                return string.Join(", ", elements);
            }
        }

        return value.ToString() ?? string.Empty;
    }

    private static bool IsGenericCollection(object obj)
    {
        if (obj == null)
        {
            return false;
        }

        // Get the type of the object
        Type type = obj.GetType();

        // Check if the type is generic
        if (type.IsGenericType)
        {
            // Get the generic type definition
            Type genericTypeDefinition = type.GetGenericTypeDefinition();

            // Check if it is a generic collection
            if (typeof(IEnumerable<>).IsAssignableFrom(genericTypeDefinition) ||
                typeof(ICollection<>).IsAssignableFrom(genericTypeDefinition) ||
                typeof(IList<>).IsAssignableFrom(genericTypeDefinition) ||
                typeof(IDictionary<,>).IsAssignableFrom(genericTypeDefinition))
            {
                return true;
            }

            // Check implemented interfaces for generic collection types
            foreach (Type implementedInterface in type.GetInterfaces())
            {
                if (implementedInterface.IsGenericType)
                {
                    Type interfaceGenericTypeDefinition = implementedInterface.GetGenericTypeDefinition();
                    if (interfaceGenericTypeDefinition == typeof(IEnumerable<>) ||
                        interfaceGenericTypeDefinition == typeof(ICollection<>) ||
                        interfaceGenericTypeDefinition == typeof(IList<>) ||
                        interfaceGenericTypeDefinition == typeof(IDictionary<,>))
                    {
                        return true;
                    }
                }
            }
        }

        return false;
    }
}
