using Endatix.Core.Infrastructure.Result;
using System.Text.Json;

namespace Endatix.Core.Models.Themes;

/// <summary>
/// Value object representing validated JSON data for themes
/// </summary>
public sealed class ThemeJsonData
{
    private readonly string _json;

    private ThemeJsonData(string json, ThemeData themeData)
    {
        _json = json;
        ThemeData = themeData;
    }

    /// <summary>
    /// Creates a new ThemeJsonData instance from a JSON string, validating it against the ThemeData structure
    /// </summary>
    /// <param name="json">The JSON string to validate</param>
    /// <returns>Success result with ThemeJsonData instance if valid, or Invalid result with error details</returns>
    public static Result<ThemeJsonData> Create(string json)
    {
        if (string.IsNullOrEmpty(json))
        {
            return Result.Invalid(new ValidationError("Theme data cannot be empty"));
        }

        try
        {
            var themeData = JsonSerializer.Deserialize<ThemeData>(json);
            if (themeData == null)
            {
                return Result.Invalid(new ValidationError("Invalid theme data structure"));
            }

            return Result.Success(new ThemeJsonData(json, themeData));
        }
        catch (JsonException ex)
        {
            return Result.Invalid(new ValidationError($"Invalid JSON: {ex.Message}"));
        }
    }

    /// <summary>
    /// Gets the validated ThemeData object
    /// </summary>
    public ThemeData ThemeData { get; }

    /// <summary>
    /// Returns the validated JSON string
    /// </summary>
    public override string ToString() => _json;
}