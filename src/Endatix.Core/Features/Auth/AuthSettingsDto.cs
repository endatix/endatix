namespace Endatix.Core.Features.Auth;

/// <summary>
/// Safe API auth settings snapshot for platform administrators.
/// </summary>
public sealed record AuthSettingsDto
{
    public required bool PlatformAdminRequiresLocalApproval { get; init; }
    public required IReadOnlyList<string> ConfigurationErrors { get; init; }
    public required IReadOnlyList<AuthProviderSettingsDto> Providers { get; init; }
}

/// <summary>
/// Safe per-provider auth settings without secrets.
/// </summary>
public sealed record AuthProviderSettingsDto
{
    public required string ProviderId { get; init; }
    public required string DisplayName { get; init; }
    public required bool IsRegistered { get; init; }
    public required bool IsEnabled { get; init; }
    public required bool IsActive { get; init; }
    public string? Issuer { get; init; }
    public IReadOnlyList<string> Audiences { get; init; } = [];
    public int? AccessExpiryMinutes { get; init; }
    public int? RefreshExpiryDays { get; init; }
    public bool? RequireHttpsMetadata { get; init; }
    public EndatixJwtProviderDetailsDto? EndatixJwt { get; init; }
    public KeycloakProviderDetailsDto? Keycloak { get; init; }
}

public sealed record EndatixJwtProviderDetailsDto
{
    public required bool SigningKeyConfigured { get; init; }
    public string? ReBacIssuer { get; init; }
    public int? FormAccessTokenExpiryMinutes { get; init; }
}

public sealed record KeycloakProviderDetailsDto
{
    public string? ClientId { get; init; }
    public required bool ClientSecretConfigured { get; init; }
    public required bool RoleMappingsConfigured { get; init; }
    public required int RoleMappingCount { get; init; }
    public string? RolesPath { get; init; }
    public required bool RejectDuplicateEmail { get; init; }
}
