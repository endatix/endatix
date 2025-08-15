using System.Text.Json;
using System.Text.Json.Nodes;

namespace Endatix.Core.Helpers;

public static class JsonHelpers
{
    private static readonly JsonSerializerOptions _jsonOptions = new() { WriteIndented = false };

    // Adds/updates a TOP-LEVEL field; preserves any existing nested JSON.
    public static string AddOrUpdateTopLevelField(string? json, string fieldName, string fieldValue)
    {
        if (string.IsNullOrWhiteSpace(fieldName))
        {
            throw new ArgumentException("Field name is required.", nameof(fieldName));
        }

        JsonObject obj;

        if (string.IsNullOrWhiteSpace(json))
        {
            obj = new JsonObject();
        }
        else
        {
            try
            {
                var root = JsonNode.Parse(json);
                obj = root as JsonObject ?? new JsonObject();
            }
            catch
            {
                obj = new JsonObject();
            }
        }

        obj[fieldName] = fieldValue;
        return obj.ToJsonString(_jsonOptions);
    }

    // Merges two top-level JSON objects. Keys from patchJson overwrite or add to originalJson. If either is invalid or not an object, it is treated as an empty object.
    public static string MergeTopLevelObject(string? originalJson, string? patchJson)
    {
        JsonObject originalObj;
        JsonObject patchObj;

        if (string.IsNullOrWhiteSpace(originalJson))
        {
            originalObj = new JsonObject();
        }
        else
        {
            try
            {
                var root = JsonNode.Parse(originalJson);
                originalObj = root as JsonObject ?? new JsonObject();
            }
            catch
            {
                originalObj = new JsonObject();
            }
        }

        if (string.IsNullOrWhiteSpace(patchJson))
        {
            patchObj = new JsonObject();
        }
        else
        {
            try
            {
                var root = JsonNode.Parse(patchJson);
                patchObj = root as JsonObject ?? new JsonObject();
            }
            catch
            {
                patchObj = new JsonObject();
            }
        }

        foreach (var kvp in patchObj)
        {
            // Clone the value to avoid "node already has a parent" when moving between objects
            var clonedValue = kvp.Value is null ? null : JsonNode.Parse(kvp.Value.ToJsonString());
            originalObj[kvp.Key] = clonedValue;
        }

        return originalObj.ToJsonString(_jsonOptions);
    }
}
