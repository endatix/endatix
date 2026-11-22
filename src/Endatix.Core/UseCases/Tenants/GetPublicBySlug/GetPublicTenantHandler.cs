using Endatix.Core.Abstractions;
using Endatix.Core.Abstractions.Repositories;
using Endatix.Core.Common;
using Endatix.Core.Infrastructure.Domain;
using Endatix.Core.Infrastructure.Messaging;
using Endatix.Core.Infrastructure.Result;
using Endatix.Core.Specifications;
using Entities = Endatix.Core.Entities;

namespace Endatix.Core.UseCases.Tenants.GetPublicBySlug;

/// <summary>
/// Loads a live tenant by opaque public id for unauthenticated sign-in / self-registration pages, with its published
/// branding when the tenant is entitled to it.
/// </summary>
public sealed class GetPublicTenantHandler(
    IRepository<Entities.Tenant> tenantRepository,
    IRepository<Entities.TenantSettings> tenantSettingsRepository,
    ITenantBrandingRepository brandingRepository,
    ITenantEntitlements entitlements)
    : IQueryHandler<GetPublicTenantQuery, Result<PublicTenantDto>>
{
    public const string TenantNotFoundMessage = "Tenant not found.";

    /// <inheritdoc/>
    public async Task<Result<PublicTenantDto>> Handle(GetPublicTenantQuery request, CancellationToken cancellationToken)
    {
        var shortUrl = ShortUrl.Normalize(request.Slug);
        if (shortUrl is null || !ShortUrl.IsValid(shortUrl))
        {
            return Result.NotFound(TenantNotFoundMessage);
        }

        var tenant = await tenantRepository.SingleOrDefaultAsync(
            new TenantSpecifications.LiveByShortUrlSpec(shortUrl),
            cancellationToken);
        if (tenant is null)
        {
            return Result.NotFound(TenantNotFoundMessage);
        }

        var settings = await tenantSettingsRepository.SingleOrDefaultAsync(
            new TenantSpecifications.SettingsByTenantIdSpec(tenant.Id),
            cancellationToken);

        return Result.Success(new PublicTenantDto(
            tenant.ShortUrl,
            tenant.Name,
            settings?.AllowSelfRegistration ?? false,
            settings?.AllowedAuthProviderKeys ?? [],
            await PublishedBrandingAsync(tenant.Id, cancellationToken)));
    }

    // A tenant without the branding entitlement shows the deployment brand, whatever it published before.
    private async Task<string?> PublishedBrandingAsync(long tenantId, CancellationToken cancellationToken) =>
        await entitlements.IsEntitledAsync(tenantId, EntitlementNames.BRANDING, cancellationToken)
            ? await brandingRepository.FindPublishedJsonAsync(tenantId, cancellationToken)
            : null;
}
