using Endatix.Core.Common;
using Endatix.Core.Infrastructure.Domain;
using Endatix.Core.Infrastructure.Messaging;
using Endatix.Core.Infrastructure.Result;
using Endatix.Core.Specifications;
using Entities = Endatix.Core.Entities;

namespace Endatix.Core.UseCases.Tenants.GetPublicBySlug;

/// <summary>
/// Loads a live tenant by opaque public id for unauthenticated sign-in / self-registration pages.
/// </summary>
public sealed class GetPublicTenantHandler(
    IRepository<Entities.Tenant> tenantRepository,
    IRepository<Entities.TenantSettings> tenantSettingsRepository)
    : IQueryHandler<GetPublicTenantQuery, Result<PublicTenantDto>>
{
    public const string TenantNotFoundMessage = "Tenant not found.";

    /// <inheritdoc/>
    public async Task<Result<PublicTenantDto>> Handle(GetPublicTenantQuery request, CancellationToken cancellationToken)
    {
        if (!PublicId.IsValidTenantSlug(request.Slug))
        {
            return Result.NotFound(TenantNotFoundMessage);
        }

        var tenant = await tenantRepository.SingleOrDefaultAsync(
            new TenantSpecifications.LiveBySlugSpec(request.Slug),
            cancellationToken);
        if (tenant is null)
        {
            return Result.NotFound(TenantNotFoundMessage);
        }

        var settings = await tenantSettingsRepository.SingleOrDefaultAsync(
            new TenantSpecifications.SettingsByTenantIdSpec(tenant.Id),
            cancellationToken);

        return Result.Success(new PublicTenantDto(
            tenant.Slug,
            tenant.Name,
            settings?.AllowSelfRegistration ?? false,
            settings?.AllowedAuthProviderKeys ?? []));
    }
}
