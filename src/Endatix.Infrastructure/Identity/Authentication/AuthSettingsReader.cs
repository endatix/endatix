using Endatix.Core.Features.Auth;
using Endatix.Core.Infrastructure.Configuration;
using Endatix.Infrastructure.Identity.Authentication.Providers;
using Microsoft.Extensions.Configuration;

namespace Endatix.Infrastructure.Identity.Authentication;

/// <summary>
/// Builds admin-safe auth settings from registered providers.
/// Mapping order: baseline from options → optional <see cref="IAuthProviderSettingsViewer"/>
/// → transitional typed mappers → generic JWT enrichment.
/// New providers should implement <see cref="IAuthProviderSettingsViewer"/> on their
/// <see cref="IAuthProvider"/> rather than extending this reader.
/// </summary>
internal sealed class AuthSettingsReader(
    AuthProviderRegistry authProviderRegistry,
    IConfiguration configuration) : IAuthSettingsReader
{
    public AuthSettingsDto GetSettings()
    {
        List<string> configurationErrors = [];
        List<AuthProviderSettingsDto> providers = [];

        foreach (ProviderRegistration registration in authProviderRegistry.GetRequestedRegistrations())
        {
            IConfigurationSection configSection = configuration.GetSection(registration.ConfigurationSectionPath);
            AuthProviderOptions? options = configSection.Get(registration.ConfigType) as AuthProviderOptions;
            bool isActive = authProviderRegistry.IsProviderActive(registration.Provider.SchemeName);
            string providerId = registration.Provider.SchemeName;

            AuthProviderSettingsDto baseline = AuthProviderSettingsMapper.CreateBaseline(
                providerId,
                options,
                isRegistered: true,
                isActive: isActive);

            if (registration.Provider is IAuthProviderSettingsViewer viewer)
            {
                providers.Add(viewer.ViewSettings(baseline, configSection, configurationErrors));
                continue;
            }

            AuthProviderSettingsDto? mapped = MapKnownProvider(
                registration,
                configSection,
                baseline,
                isActive,
                configurationErrors);

            if (mapped is not null)
            {
                providers.Add(mapped);
                continue;
            }

            if (options is JwtAuthProviderOptions jwtOptions)
            {
                providers.Add(AuthProviderSettingsMapper.ApplyJwtFields(baseline, jwtOptions));
                continue;
            }

            providers.Add(baseline);
        }

        return new AuthSettingsDto
        {
            PlatformAdminRequiresLocalApproval = true,
            ConfigurationErrors = configurationErrors,
            Providers = providers,
        };
    }

    private static AuthProviderSettingsDto? MapKnownProvider(
        ProviderRegistration registration,
        IConfigurationSection configSection,
        AuthProviderSettingsDto baseline,
        bool isActive,
        List<string> configurationErrors)
    {
        if (registration.ConfigType == typeof(EndatixJwtOptions))
        {
            EndatixJwtOptions? endatixOptions = AuthProviderSettingsMapper.BindOptions<EndatixJwtOptions>(configSection);

            return registration.Provider.SchemeName == AuthSchemes.EndatixReBac
                ? MapEndatixReBacProvider(endatixOptions, baseline, isActive, configurationErrors)
                : MapEndatixJwtProvider(endatixOptions, baseline, isActive, configurationErrors);
        }

        if (registration.ConfigType == typeof(KeycloakOptions))
        {
            KeycloakOptions? keycloakOptions = AuthProviderSettingsMapper.BindOptions<KeycloakOptions>(configSection);
            return MapKeycloakProvider(keycloakOptions, baseline, isActive, configurationErrors);
        }

        return null;
    }

    private static AuthProviderSettingsDto MapEndatixJwtProvider(
        EndatixJwtOptions? options,
        AuthProviderSettingsDto baseline,
        bool isActive,
        List<string> configurationErrors)
    {
        if (options is null)
        {
            configurationErrors.Add("EndatixJwt provider configuration is missing.");
            return AuthProviderSettingsMapper.CreateBaseline(
                AuthSchemes.EndatixJwt,
                options: null,
                isRegistered: true,
                isActive: false,
                displayNameOverride: "Endatix JWT");
        }

        if (!SettingsSanitizer.HasSecret(options.SigningKey))
        {
            configurationErrors.Add("EndatixJwt signing key is not configured.");
        }

        return new AuthProviderSettingsDto
        {
            ProviderId = AuthSchemes.EndatixJwt,
            DisplayName = ResolveKnownProviderDisplayName(baseline, "Endatix JWT"),
            IsRegistered = baseline.IsRegistered,
            IsEnabled = baseline.IsEnabled,
            IsActive = isActive,
            Issuer = options.Issuer,
            Audiences = options.Audiences.ToList(),
            AccessExpiryMinutes = options.AccessExpiryInMinutes,
            RefreshExpiryDays = options.RefreshExpiryInDays,
            EndatixJwt = new EndatixJwtProviderDetailsDto
            {
                SigningKeyConfigured = SettingsSanitizer.HasSecret(options.SigningKey),
                ReBacIssuer = options.ReBacIssuer,
                FormAccessTokenExpiryMinutes = options.FormAccessTokenExpiryMinutes,
            },
        };
    }

    private static AuthProviderSettingsDto MapEndatixReBacProvider(
        EndatixJwtOptions? options,
        AuthProviderSettingsDto baseline,
        bool isActive,
        List<string> configurationErrors)
    {
        if (options is null)
        {
            configurationErrors.Add("EndatixReBac provider configuration is missing.");
            return AuthProviderSettingsMapper.CreateBaseline(
                AuthSchemes.EndatixReBac,
                options: null,
                isRegistered: true,
                isActive: false,
                displayNameOverride: "Endatix ReBAC JWT");
        }

        return new AuthProviderSettingsDto
        {
            ProviderId = AuthSchemes.EndatixReBac,
            DisplayName = ResolveKnownProviderDisplayName(baseline, "Endatix ReBAC JWT"),
            IsRegistered = baseline.IsRegistered,
            IsEnabled = baseline.IsEnabled,
            IsActive = isActive,
            Issuer = options.ReBacIssuer,
            Audiences = options.Audiences.ToList(),
            AccessExpiryMinutes = options.FormAccessTokenExpiryMinutes,
            EndatixJwt = new EndatixJwtProviderDetailsDto
            {
                SigningKeyConfigured = SettingsSanitizer.HasSecret(options.SigningKey),
                ReBacIssuer = options.ReBacIssuer,
                FormAccessTokenExpiryMinutes = options.FormAccessTokenExpiryMinutes,
            },
        };
    }

    private static AuthProviderSettingsDto MapKeycloakProvider(
        KeycloakOptions? options,
        AuthProviderSettingsDto baseline,
        bool isActive,
        List<string> configurationErrors)
    {
        if (options is null)
        {
            configurationErrors.Add("Keycloak provider configuration is missing.");
            return AuthProviderSettingsMapper.CreateBaseline(
                AuthProviders.Keycloak,
                options: null,
                isRegistered: true,
                isActive: false,
                displayNameOverride: "Keycloak");
        }

        if (baseline.IsEnabled && string.IsNullOrWhiteSpace(options.Issuer))
        {
            configurationErrors.Add("Keycloak issuer is not configured.");
        }

        if (baseline.IsEnabled && !SettingsSanitizer.HasSecret(options.ClientSecret))
        {
            configurationErrors.Add("Keycloak client secret is not configured.");
        }

        int roleMappingCount = options.Authorization?.RoleMappings?.Count ?? 0;

        return new AuthProviderSettingsDto
        {
            ProviderId = AuthProviders.Keycloak,
            DisplayName = ResolveKnownProviderDisplayName(baseline, "Keycloak"),
            IsRegistered = baseline.IsRegistered,
            IsEnabled = baseline.IsEnabled,
            IsActive = isActive,
            Issuer = options.Issuer,
            Audiences = AuthProviderSettingsMapper.ApplyJwtFields(baseline, options).Audiences,
            RequireHttpsMetadata = options.RequireHttpsMetadata,
            Keycloak = new KeycloakProviderDetailsDto
            {
                ClientId = options.ClientId,
                ClientSecretConfigured = SettingsSanitizer.HasSecret(options.ClientSecret),
                RoleMappingsConfigured = roleMappingCount > 0,
                RoleMappingCount = roleMappingCount,
                RolesPath = options.Authorization?.RolesPath,
                RejectDuplicateEmail = options.Provisioning.RejectDuplicateEmail,
            },
        };
    }

    private static string ResolveKnownProviderDisplayName(
        AuthProviderSettingsDto baseline,
        string fallbackDisplayName) =>
        baseline.DisplayName != baseline.ProviderId
            ? baseline.DisplayName
            : fallbackDisplayName;
}
