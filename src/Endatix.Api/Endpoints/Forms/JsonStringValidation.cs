using System.Text.Json;

namespace Endatix.Api.Endpoints.Forms;

internal static class JsonStringValidation
{
    internal static bool IsValid(string? json)
    {
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
