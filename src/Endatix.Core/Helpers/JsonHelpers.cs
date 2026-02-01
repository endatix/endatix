using System.Text.Json;
using System.Text.Json.Nodes;

namespace Endatix.Core.Helpers;

public static class JsonHelpers
{
    private static readonly JsonSerializerOptions _jsonOptions = new() { WriteIndented = false };

    /// <summary>
    /// Merges two top-level JSON objects. Keys from patchJson overwrite or add to originalJson. If either is invalid or not an object, it is treated as an empty object.
    /// </summary>
    /// <param name="originalJson"></param>
    /// <param name="patchJson"></param>
    /// <returns></returns>
    public static string MergeTopLevelObject(string? originalJson, string? patchJson)
    {
        JsonObject originalObj;
        JsonObject patchObj;

        if (string.IsNullOrWhiteSpace(originalJson))
        {
            originalObj = [];
        }
        else
        {
            try
            {
                var root = JsonNode.Parse(originalJson);
                originalObj = root as JsonObject ?? [];
            }
            catch
            {
                originalObj = [];
            }
        }

        if (string.IsNullOrWhiteSpace(patchJson))
        {
            patchObj = [];
        }
        else
        {
            try
            {
                var root = JsonNode.Parse(patchJson);
                patchObj = root as JsonObject ?? [];
            }
            catch
            {
                patchObj = [];
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
