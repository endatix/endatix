using System.Text.Json;

namespace Endatix.Api.Common;

public static class JsonStringValidation
{
    /// <summary>
    /// Validates that the string is a valid JSON string.
    /// </summary>
    /// <param name="json">The JSON string to validate.</param>
    /// <returns>True if the string is a valid JSON string, false otherwise.</returns>
    public static bool IsValid(string? json)
    {
        if (string.IsNullOrEmpty(json))
        {
            return true;
        }

        try
        {
            using var document = JsonDocument.Parse(json!);
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }
}
