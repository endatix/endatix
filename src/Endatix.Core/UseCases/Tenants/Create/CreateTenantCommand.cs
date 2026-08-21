using Endatix.Core.Infrastructure.Messaging;
using Endatix.Core.Infrastructure.Result;

namespace Endatix.Core.UseCases.Tenants.Create;

/// <summary>
/// Creates a tenant together with its settings. Platform-scoped: no membership is granted to the caller.
/// The short URL is generated server-side and is not accepted from the client.
/// </summary>
/// <param name="Name">Display name.</param>
/// <param name="Description">Optional description.</param>
    /// <param name="AllowSelfRegistration">Whether anonymous users may self-register via the tenant short URL.</param>
/// <param name="AllowedAuthProviderKeys">Host auth provider keys allowed for self-registration. Null means none.</param>
/// <param name="DefaultRegistrationRoleName">Role assigned on self-registration. Null uses the persisted default.</param>
public sealed record CreateTenantCommand(
    string Name,
    string? Description = null,
    bool AllowSelfRegistration = false,
    IReadOnlyList<string>? AllowedAuthProviderKeys = null,
    string? DefaultRegistrationRoleName = null) : ICommand<Result<TenantDto>>;
