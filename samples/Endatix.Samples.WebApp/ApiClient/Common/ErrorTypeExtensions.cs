using System.ComponentModel.DataAnnotations;
using System.Reflection;

namespace Endatix.Samples.WebApp.ApiClient.Common;

/// <summary>
/// Provides extension methods for the ErrorType enum to retrieve their display names.
/// </summary>
public static class ErrorTypeExtensions
{
    /// <summary>
    /// Retrieves the display name of an ErrorType enum value.
    /// </summary>
    /// <param name="errorType">The ErrorType enum value to retrieve the display name for.</param>
    /// <returns>The display name of the ErrorType enum value, or its string representation if no display name is found.</returns>
    public static string GetDisplayName(this ErrorType errorType)
    {
        var displayAttribute = errorType.GetType()
            .GetMember(errorType.ToString())
            .FirstOrDefault()?
            .GetCustomAttribute<DisplayAttribute>();

        return displayAttribute?.Name ?? errorType.ToString();
    }
}