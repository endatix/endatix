using System.Text.Json;
using Ardalis.GuardClauses;
using Endatix.Core.Infrastructure.Result;

namespace Endatix.Infrastructure.Utils;

/// <summary>
/// Utility class to extract JSON data from a string.
/// </summary>
public class JsonExtractor : IDisposable
{
    private readonly JsonDocument _document;
    private bool _disposed = false;

    /// <summary>
    /// Initializes a new instance of the <see cref="JsonExtractor"/> class.
    /// </summary>
    /// <param name="content">The JSON content to extract.</param>
    /// <exception cref="InvalidOperationException">Thrown when the JSON content is invalid or parsing fails.</exception>
    public JsonExtractor(string content)
    {
        Guard.Against.NullOrWhiteSpace(content, nameof(content));

        try
        {
            _document = JsonDocument.Parse(content);
        }
        catch (JsonException ex)
        {
            throw new InvalidOperationException($"Failed to parse JSON: {ex.Message}", ex);
        }
    }


    /// <summary>
    /// Gets the root element of the JSON document.
    /// </summary>
    public JsonElement RootElement => _document.RootElement;

    /// <summary>
    /// Extracts an array of strings from the JSON document.
    /// </summary>
    /// <param name="path">The .notation JSON path to the array of strings e.g. "resource_access.endatix-hub.roles".</param>
    /// <returns>Result with the array of strings or an error if the array is not found.</returns>
    public Result<string[]> ExtractArrayOfStrings(string path)
    {
        if (string.IsNullOrEmpty(path))
        {
            return Result<string[]>.Invalid(new ValidationError("Path is required"));
        }

        var getElementResult = GetNestedElement(RootElement, path);
        if (!getElementResult.IsSuccess)
        {
            return Result.NotFound();
        }

        try
        {
            var stringValues = getElementResult.Value
                .EnumerateArray()
                .Select(x => x.ValueKind == JsonValueKind.String ? x.GetString()! : string.Empty)
                .ToArray();

            return Result.Success(stringValues);
        }
        catch (InvalidOperationException)
        {
            return Result.Invalid(new ValidationError("Selected object is not JSON array"));
        }
        catch (Exception)
        {
            return Result.Error("Failed to extract array of strings");
        }
    }


    /// <summary>
    /// Gets the element by the given JSON path.
    /// </summary>
    /// <param name="element">The element to get the element from.</param>
    /// <param name="jsonPath">The JSON path to the property using dot notation e.g. "resource_access.endatix-hub.roles".</param>
    /// <returns>Result with the property value or an error if the property is not found.</returns>
    private Result<JsonElement> GetNestedElement(JsonElement element, string jsonPath)
    {
        var propertyNames = jsonPath.Split('.');
        var currentElement = element;
        foreach (var name in propertyNames)
        {
            if (currentElement.TryGetProperty(name, out var property))
            {
                currentElement = property;
            }
            else
            {
                return Result<JsonElement>.NotFound("Property not found");
            }
        }

        return Result<JsonElement>.Success(currentElement);
    }


    public void Dispose()
    {
        if (!_disposed)
        {
            _document?.Dispose();
            _disposed = true;
        }
    }
}