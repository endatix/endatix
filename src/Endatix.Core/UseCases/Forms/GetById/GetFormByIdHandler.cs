using Endatix.Core.Abstractions;
using Endatix.Core.Abstractions.Authorization;
using Endatix.Core.Abstractions.Repositories;
using Endatix.Core.Infrastructure.Messaging;
using Endatix.Core.Infrastructure.Result;
using Endatix.Core.Specifications;

namespace Endatix.Core.UseCases.Forms.GetById;

/// <summary>Reads a visible form with its submission count.</summary>
public class GetFormByIdHandler(IFormsRepository repository, ITenantContext tenant) : IQueryHandler<GetFormByIdQuery, Result<FormDto>>
{
    public async Task<Result<FormDto>> Handle(GetFormByIdQuery request, CancellationToken cancellationToken)
    {
        if (tenant is { TenantId: <= 0 })
        {
            return Result.NotFound("Form not found.");
        }
        var spec = new FormByIdWithSubmissionsCountSpec(request.FormId);
        spec.InTenant(tenant);
        var form = await repository.FirstOrDefaultAsync(spec, cancellationToken);
        if (form == null)
        {
            return Result.NotFound("Form not found.");
        }
        return Result.Success(form);
    }
}
