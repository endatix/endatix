using Endatix.Core.Abstractions;
using Endatix.Core.Entities;
using Endatix.Core.Infrastructure.Domain;
using Endatix.Core.Infrastructure.Messaging;
using Endatix.Core.Infrastructure.Result;
using Endatix.Core.Specifications;

namespace Endatix.Core.UseCases.Identity.ListMyTenants;

/// <summary>
/// Returns the current user's memberships. Active is last-used <c>AppUser.TenantId</c> (JWT <c>tid</c> while not assumed).
/// </summary>
public sealed class ListMyTenantsHandler(
    IUserContext userContext,
    IUserService userService,
    IRepository<Tenant> tenantRepository)
    : IQueryHandler<ListMyTenantsQuery, Result<UserTenantsDto>>
{
    /// <inheritdoc />
    public async Task<Result<UserTenantsDto>> Handle(
        ListMyTenantsQuery request,
        CancellationToken cancellationToken)
    {
        var actor = userContext.GetCurrentUser();
        if (actor is null)
        {
            return Result.Unauthorized();
        }

        var membershipResult = await userService.ListMembershipTenantIdsAsync(actor.Id, cancellationToken);
        if (!membershipResult.IsSuccess)
        {
            return membershipResult.ToErrorResult<UserTenantsDto>();
        }

        var tenantIds = membershipResult.Value;
        if (tenantIds.Count == 0)
        {
            return Result.Success(new UserTenantsDto([]));
        }

        var tenants = await tenantRepository.ListAsync(
            new TenantSpecifications.ByIdsSpec(tenantIds),
            cancellationToken);

        var activeTenantId = actor.TenantId;
        var items = tenants
            .OrderBy(tenant => tenant.Name)
            .Select(tenant => new UserTenantDto(
                tenant.Id,
                tenant.Name,
                tenant.ShortUrl,
                tenant.Id == activeTenantId))
            .ToList();

        return Result.Success(new UserTenantsDto(items));
    }
}
