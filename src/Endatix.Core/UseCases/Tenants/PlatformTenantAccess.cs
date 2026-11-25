using System.Diagnostics.CodeAnalysis;
using Endatix.Core.Abstractions.Authorization;
using Endatix.Core.Infrastructure.Domain;
using Endatix.Core.Infrastructure.Messaging;
using Endatix.Core.Infrastructure.Result;
using Endatix.Core.Specifications;

namespace Endatix.Core.UseCases.Tenants;

/// <summary>Checks an operator request before it reads or changes tenant data.</summary>
[Experimental("ENDATIX0006")]
public sealed record PlatformTenantAccess(long? TenantId = null, bool RequiresSettings = false) : IQuery<Result>;

internal sealed class PlatformTenantAccessHandler(
    IRepository<Entities.Tenant> tenants, IRepository<Entities.TenantSettings> settings, AccountStanding standing)
    : IQueryHandler<PlatformTenantAccess, Result>
{
    public async Task<Result> Handle(PlatformTenantAccess request, CancellationToken cancellationToken)
    {
        if (!standing.IsEnforced)
        {
            return Result.Success();
        }
        if (request.TenantId is long id)
        {
            if (!await tenants.AnyAsync(new TenantSpecifications.ByIdSpec(id), cancellationToken))
            {
                return Result.NotFound("Tenant not found.");
            }
            if (request.RequiresSettings && !await settings.AnyAsync(new TenantSpecifications.SettingsByTenantIdSpec(id), cancellationToken))
            {
                return Result.NotFound("Tenant settings not found.");
            }
        }
        return await standing.RefusalAsync(cancellationToken) ?? Result.Success();
    }
}
