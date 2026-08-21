using Endatix.Core.Infrastructure.Domain;
using Endatix.Core.Infrastructure.Messaging;
using Endatix.Core.Infrastructure.Result;
using Endatix.Core.Specifications;
using Entities = Endatix.Core.Entities;

namespace Endatix.Core.UseCases.Tenants.GetById;

/// <summary>
/// Handler for loading a platform tenant by id.
/// </summary>
public sealed class GetTenantByIdHandler(
    IRepository<Entities.Tenant> tenantRepository,
    IRepository<Entities.TenantSettings> tenantSettingsRepository)
    : IQueryHandler<GetTenantByIdQuery, Result<TenantDto>>
{
    /// <inheritdoc/>
    public async Task<Result<TenantDto>> Handle(GetTenantByIdQuery request, CancellationToken cancellationToken)
    {
        var tenant = await tenantRepository.SingleOrDefaultAsync(
            new TenantSpecifications.ByIdSpec(request.TenantId),
            cancellationToken);
        if (tenant is null)
        {
            return Result.NotFound("Tenant not found.");
        }

        var settings = await tenantSettingsRepository.SingleOrDefaultAsync(
            new TenantSpecifications.SettingsByTenantIdSpec(request.TenantId),
            cancellationToken);

        return Result.Success(TenantDto.FromEntity(tenant, settings));
    }
}
