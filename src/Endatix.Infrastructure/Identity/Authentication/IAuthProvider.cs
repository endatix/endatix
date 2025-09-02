using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

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
    /// Gets the configuration section path for this provider.
    /// Defaults to "Endatix:Auth:Providers:{SchemeName}"
    /// </summary>
    string ConfigurationSectionPath => $"Endatix:Auth:Providers:{SchemeName}";

    /// <summary>
    /// Checks if the provider can handle the given token.
    /// </summary>
    /// <param name="issuer">The issuer of the token.</param>
    /// <param name="rawToken">The raw token string (without "Bearer " prefix).</param>
    /// <returns>True if the provider can handle the given token, false otherwise.</returns>
    bool CanHandle(string issuer, string rawToken);


    /// <summary>
    /// Called at startup to configure authentication for this provider.
    /// The configuration section passed is specific to this provider.
    /// </summary>
    void Configure(AuthenticationBuilder builder, IConfigurationSection providerConfig, bool isDevelopment = false);

    /// <summary>
    /// Register any additional services required by this provider.
    /// Called during service registration phase.
    /// </summary>
    void RegisterServices(IServiceCollection services, IConfiguration configuration) { }
}
