using Endatix.Core.Exceptions;
using Endatix.Core.Infrastructure.Domain;
using Endatix.Core.Infrastructure.Result;
using Endatix.Core.Specifications;

namespace Endatix.Core.UseCases.TenantBranding;

/// <summary>
/// The save every write of a tenant's branding shares: load the row (or start one), apply the change, and save only
/// when the revision moved, turning a lost race into a conflict.
/// </summary>
internal static class TenantBrandingWriter
{
    internal static async Task<Result<TenantBrandingDto>> WriteAsync(
        IRepository<Entities.TenantBranding> repository,
        long tenantId,
        Func<Entities.TenantBranding, Result> change,
        CancellationToken cancellationToken)
    {
        var branding = await repository.FirstOrDefaultAsync(new TenantBrandingByTenantIdSpec(tenantId), cancellationToken);
        var isNew = branding is null;
        branding ??= new Entities.TenantBranding(tenantId);
        var revision = branding.Revision;

        var result = change(branding);
        if (!result.IsSuccess || branding.Revision == revision)
        {
            return result.IsSuccess ? TenantBrandingDto.From(branding) : result;
        }

        try
        {
            if (isNew)
            {
                await repository.AddAsync(branding, cancellationToken);
            }
            else
            {
                await repository.UpdateAsync(branding, cancellationToken);
            }
        }
        catch (TenantBrandingConflictException)
        {
            return Result.Conflict("The branding changed while it was being saved; read it again and retry.");
        }

        return TenantBrandingDto.From(branding);
    }
}
