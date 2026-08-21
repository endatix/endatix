using Endatix.Core.Infrastructure.Messaging;
using Endatix.Core.Infrastructure.Result;

namespace Endatix.Core.UseCases.Tenants.Create;

/// <summary>
/// Creates a tenant together with its settings. Platform-scoped: no membership is granted to the caller.
/// </summary>
/// <param name="Name">Display name.</param>
/// <param name="Slug">Requested public slug. Normalized and checked for uniqueness by the handler.</param>
/// <param name="Description">Optional description.</param>
/// <param name="AllowSelfRegistration">Whether anonymous users may self-register via the tenant slug.</param>
/// <param name="AllowedAuthProviderKeys">Host auth provider keys allowed for self-registration. Null means none.</param>
/// <param name="DefaultRegistrationRoleName">Role assigned on self-registration. Null uses the persisted default.</param>
public sealed record CreateTenantCommand(
    string Name,
    string Slug,
    string? Description = null,
    bool AllowSelfRegistration = false,
    IReadOnlyList<string>? AllowedAuthProviderKeys = null,
    string? DefaultRegistrationRoleName = null) : ICommand<Result<TenantDto>>;
