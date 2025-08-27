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
    /// Implementations should read their config from the given section.
    /// </summary>
    void Configure(AuthenticationBuilder builder, IConfiguration providerConfig, bool isDevelopment = false);
}
