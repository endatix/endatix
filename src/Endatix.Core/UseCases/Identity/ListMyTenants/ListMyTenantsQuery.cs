using Endatix.Core.Infrastructure.Messaging;
using Endatix.Core.Infrastructure.Result;

namespace Endatix.Core.UseCases.Identity.ListMyTenants;

/// <summary>
/// Lists tenants the current user may switch into.
/// </summary>
public sealed record ListMyTenantsQuery : IQuery<Result<UserTenantsDto>>;

/// <summary>
/// Membership tenants for the switcher. Numeric id is authenticated-only.
/// </summary>
public sealed record UserTenantsDto(IReadOnlyList<UserTenantDto> Items);

/// <summary>
/// One membership tenant.
/// </summary>
public sealed record UserTenantDto(long Id, string Name, string Slug, bool IsActive);
