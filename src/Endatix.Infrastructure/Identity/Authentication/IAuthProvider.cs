using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Configuration;

namespace Endatix.Infrastructure.Identity.Authentication;

/// <summary>
/// Defines the contract for authentication providers in the Endatix platform.
/// Allows for pluggable authentication schemes that can handle different token issuers.
/// </summary>
public interface IAuthProvider
{
    /// <summary>Unique scheme name (matches the config section key).</summary>
    string SchemeName { get; }

    /// <summary>
    /// Called at startup to configure authentication for this provider.
    /// The configuration section passed is specific to this provider.
    /// </summary>
    void Configure(AuthenticationBuilder builder, IConfigurationSection providerConfig, bool isDevelopment = false);
}
