using Endatix.Core.Abstractions.Authorization;
using Endatix.Core.Abstractions.Repositories;
using Endatix.Core.Infrastructure.Messaging;
using Endatix.Core.Infrastructure.Result;
using Endatix.Core.Specifications;

namespace Endatix.Core.UseCases.Forms.GetById;

/// <summary>Reads a visible form with its submission count.</summary>
public class GetFormByIdHandler(IFormsRepository repository, AccountStanding standing) : IQueryHandler<GetFormByIdQuery, Result<FormDto>>
{
    /// <summary>Creates a handler with dormant account standing.</summary>
    public GetFormByIdHandler(IFormsRepository repository) : this(repository, new AccountStanding()) { }

    public async Task<Result<FormDto>> Handle(GetFormByIdQuery request, CancellationToken cancellationToken)
    {
        if (standing.IsEnforced && !await repository.AnyAsync(new FormSpecifications.ByIdReadOnly(request.FormId), cancellationToken))
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
