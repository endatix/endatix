using Endatix.Core.Features.Auth;
using Endatix.Infrastructure.Identity.Authentication.Providers;
using Microsoft.Extensions.Configuration;

namespace Endatix.Infrastructure.Identity.Authentication;

/// <summary>
/// Shared mapping from registered provider options to admin-safe DTO fields.
/// Provider-specific secrets and extensions belong in <see cref="IAuthProviderSettingsViewer"/>.
/// </summary>
internal static class AuthProviderSettingsMapper
{
    public static AuthProviderSettingsDto CreateBaseline(
        string providerId,
        AuthProviderOptions? options,
        bool isRegistered,
        bool isActive,
        string? displayNameOverride = null)
    {
        return new AuthProviderSettingsDto
        {
            ProviderId = providerId,
            DisplayName = !string.IsNullOrWhiteSpace(displayNameOverride)
                ? displayNameOverride
                : ResolveDisplayName(providerId, options),
            IsRegistered = isRegistered,
            IsEnabled = options?.Enabled ?? false,
            IsActive = isActive,
            RequireHttpsMetadata = options?.RequireHttpsMetadata,
        };
    }

    public static AuthProviderSettingsDto ApplyJwtFields(
        AuthProviderSettingsDto baseline,
        JwtAuthProviderOptions options) =>
        new()
        {
            ProviderId = baseline.ProviderId,
            DisplayName = baseline.DisplayName,
            IsRegistered = baseline.IsRegistered,
            IsEnabled = baseline.IsEnabled,
            IsActive = baseline.IsActive,
            Issuer = options.Issuer,
            Audiences = ResolveAudiences(options),
            AccessExpiryMinutes = baseline.AccessExpiryMinutes,
            RefreshExpiryDays = baseline.RefreshExpiryDays,
            RequireHttpsMetadata = options.RequireHttpsMetadata,
            EndatixJwt = baseline.EndatixJwt,
            Keycloak = baseline.Keycloak,
        };

    public static TOptions? BindOptions<TOptions>(IConfigurationSection section)
        where TOptions : class, new() =>
        section.Get<TOptions>();

    public static AuthProviderSettingsDto CopyBaseline(AuthProviderSettingsDto baseline) =>
        new()
        {
            ProviderId = baseline.ProviderId,
            DisplayName = baseline.DisplayName,
            IsRegistered = baseline.IsRegistered,
            IsEnabled = baseline.IsEnabled,
            IsActive = baseline.IsActive,
            Issuer = baseline.Issuer,
            Audiences = baseline.Audiences,
            AccessExpiryMinutes = baseline.AccessExpiryMinutes,
            RefreshExpiryDays = baseline.RefreshExpiryDays,
            RequireHttpsMetadata = baseline.RequireHttpsMetadata,
            EndatixJwt = baseline.EndatixJwt,
            Keycloak = baseline.Keycloak,
        };

    private static string ResolveDisplayName(string providerId, AuthProviderOptions? options)
    {
        if (!string.IsNullOrWhiteSpace(options?.DisplayName))
        {
            return options.DisplayName;
        }

        return providerId;
    }

    private static IReadOnlyList<string> ResolveAudiences(JwtAuthProviderOptions options)
    {
        if (options.Audiences.Count > 0)
        {
            return options.Audiences.ToList();
        }

        return options switch
        {
            KeycloakOptions keycloak when !string.IsNullOrWhiteSpace(keycloak.Audience) =>
                [keycloak.Audience],
            GoogleOptions google when !string.IsNullOrWhiteSpace(google.Audience) =>
                [google.Audience],
            _ => [],
        };
    }
}
