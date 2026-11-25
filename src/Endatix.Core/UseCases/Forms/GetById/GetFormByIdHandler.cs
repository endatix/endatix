using Endatix.Core.Abstractions;
using Endatix.Core.Abstractions.Authorization;
using Endatix.Core.Abstractions.Repositories;
using Endatix.Core.Infrastructure.Messaging;
using Endatix.Core.Infrastructure.Result;
using Endatix.Core.Specifications;

namespace Endatix.Core.UseCases.Forms.GetById;

/// <summary>Reads a visible form with its submission count.</summary>
public class GetFormByIdHandler(IFormsRepository repository, AccountStanding standing, ITenantContext? tenant) : IQueryHandler<GetFormByIdQuery, Result<FormDto>>
{
    /// <summary>Creates a handler with dormant account standing.</summary>
    public GetFormByIdHandler(IFormsRepository repository) : this(repository, new AccountStanding(), null) { }

    public async Task<Result<FormDto>> Handle(GetFormByIdQuery request, CancellationToken cancellationToken)
    {
        if (standing.IsEnforced && (tenant is not { TenantId: > 0 }
            || !await repository.AnyAsync(new FormSpecifications.ByIdForTenant(request.FormId, tenant.TenantId), cancellationToken)))
        {
            return Result.NotFound("Form not found.");
        }
        return await standing.RefusalAsync<FormDto>(cancellationToken) ?? await ReadAsync(request, cancellationToken);
    }

    private async Task<Result<FormDto>> ReadAsync(GetFormByIdQuery request, CancellationToken cancellationToken)
    {
        var spec = new FormByIdWithSubmissionsCountSpec(request.FormId);
        var form = await repository.FirstOrDefaultAsync(spec, cancellationToken);
        if (form == null)
        {
            return Result.NotFound("Form not found.");
        }
        return Result.Success(form);
    }
}
