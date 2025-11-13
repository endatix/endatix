namespace Endatix.Infrastructure.Identity.Providers;

/// <summary>
/// Base class for authorization strategy options.
/// </summary>
public abstract class AuthorizationStrategyOptionsBase
{
    /// <summary>
    /// The name of the authorization strategy.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// The role mappings for the authorization strategy.
    /// </summary>
    public Dictionary<string, string> RoleMappings { get; set; } = new();
}