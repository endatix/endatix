namespace Endatix.Api;

/// <summary>
/// Routes for the Endatix API.
/// </summary>
public static class ApiRoutes
{
    /// <summary>
    /// Returns the public route for the given endpoint route.
    /// </summary>
    /// <param name="route">The route for the endpoint.</param>
    /// <returns>The public route for the endpoint.</returns>
    /// <example>
    /// <code>
    /// var publicRoute = Routes.Public("data-lists");
    /// // returns "public/data-lists"
    /// </code>
    /// </example>
    public static string Public(string route) => $"public/{route}";
}