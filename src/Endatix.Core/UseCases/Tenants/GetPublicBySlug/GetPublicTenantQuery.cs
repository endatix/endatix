using Endatix.Core.Infrastructure.Messaging;
using Endatix.Core.Infrastructure.Result;

namespace Endatix.Core.UseCases.Tenants.GetPublicBySlug;

/// <summary>
/// Unauthenticated lookup of a live tenant by opaque public id. Does not return the numeric id.
/// </summary>
public sealed record GetPublicTenantQuery(string Slug) : IQuery<Result<PublicTenantDto>>;

/// <summary>
/// Public tenant discovery DTO. Numeric id is omitted on purpose.
/// </summary>
public sealed record PublicTenantDto(
    string Slug,
    string Name,
    bool SelfRegistrationEnabled,
    IReadOnlyList<string> AllowedAuthProviders);
