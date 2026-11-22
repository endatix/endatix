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
/// <param name="Slug">The opaque public id.</param>
/// <param name="Name">The tenant's display name.</param>
/// <param name="SelfRegistrationEnabled">Whether people may register themselves.</param>
/// <param name="AllowedAuthProviders">The sign-in providers self-registration allows.</param>
/// <param name="BrandingJson">The tenant's published brand override; null when nothing is published or the tenant is
/// not entitled to branding. The <c>WhiteLabeling</c> flag is the caller's to apply.</param>
public sealed record PublicTenantDto(
    string Slug,
    string Name,
    bool SelfRegistrationEnabled,
    IReadOnlyList<string> AllowedAuthProviders,
    string? BrandingJson = null);
