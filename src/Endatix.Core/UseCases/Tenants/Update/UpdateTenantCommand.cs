using Endatix.Core.Infrastructure.Messaging;
using Endatix.Core.Infrastructure.Result;

namespace Endatix.Core.UseCases.Tenants.Update;

/// <summary>
/// Partially updates a tenant and its self-registration policy. The slug is immutable and cannot be changed.
/// </summary>
/// <param name="TenantId">The tenant to update.</param>
/// <param name="Name">When set, the new display name.</param>
/// <param name="Description">When set, the new description. An empty string clears it.</param>
/// <param name="AllowSelfRegistration">When set, toggles anonymous self-registration.</param>
/// <param name="AllowedAuthProviderKeys">When set, replaces the allowed provider keys. An empty list clears them.</param>
/// <param name="DefaultRegistrationRoleName">When set, the role assigned on self-registration.</param>
public sealed record UpdateTenantCommand(
    long TenantId,
    string? Name = null,
    string? Description = null,
    bool? AllowSelfRegistration = null,
    IReadOnlyList<string>? AllowedAuthProviderKeys = null,
    string? DefaultRegistrationRoleName = null) : ICommand<Result<TenantDto>>;
