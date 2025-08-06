using Endatix.Framework.Configuration;

namespace Endatix.Infrastructure.Identity.Authentication;

/// <summary>
/// Root configuration options for the Endatix authentication system.
/// Supports multiple authentication providers and configuration-driven setup.
/// </summary>
public class AuthenticationOptions : EndatixOptionsBase
{
    /// <summary>
    /// The configuration section path for these options.
    /// </summary>
    public override string SectionPath => "Authentication";

    /// <summary>
    /// Collection of authentication provider configurations.
    /// Each provider can be individually enabled/disabled and configured.
    /// </summary>
    public List<AuthProviderOptions> Providers { get; set; } = new();

    /// <summary>
    /// The default authentication scheme to use when no provider matches.
    /// Defaults to the Endatix JWT scheme.
    /// </summary>
    public string DefaultScheme { get; set; } = AuthSchemes.Endatix;

    /// <summary>
    /// Whether to enable automatic provider discovery and registration.
    /// When true, built-in providers will be automatically registered.
    /// </summary>
    public bool EnableAutoDiscovery { get; set; } = true;
} 