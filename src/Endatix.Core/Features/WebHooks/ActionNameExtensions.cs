using System.ComponentModel.DataAnnotations;
using System.Reflection;

namespace Endatix.Core.Features.WebHooks;

/// <summary>
/// Provides extension methods for the ActionNames enum to retrieve their display names.
/// </summary>
public static class ActionNameExtensions
{
    /// <summary>
    /// Retrieves the display name of an ActionNames enum value.
    /// </summary>
    /// <param name="action">The ActionNames enum value to retrieve the display name for.</param>
    /// <returns>The display name of the ActionNames enum value, or its string representation if no display name is found.</returns>
    public static string GetDisplayName(this ActionName action)
    {
        var displayAttribute = action.GetType()
            .GetMember(action.ToString())
            .FirstOrDefault()?
            .GetCustomAttribute<DisplayAttribute>();

        return displayAttribute?.Name ?? action.ToString();
    }
}