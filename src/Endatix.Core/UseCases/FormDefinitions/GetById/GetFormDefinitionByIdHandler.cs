using Endatix.Core.Abstractions.Repositories;
using Endatix.Core.Entities;
using Endatix.Core.Infrastructure.Messaging;
using Endatix.Core.Infrastructure.Result;

namespace Endatix.Core.UseCases.FormDefinitions.GetById;

public class GetFormDefinitionByIdHandler(IFormsRepository formsRepository,
    ManagedFormContentAccess? access) : IQueryHandler<GetFormDefinitionByIdQuery, Result<FormDefinition>>
{
    /// <summary>Creates a handler with dormant account standing.</summary>
    public GetFormDefinitionByIdHandler(IFormsRepository formsRepository)
        : this(formsRepository, null) { }

    public async Task<Result<FormDefinition>> Handle(GetFormDefinitionByIdQuery request, CancellationToken cancellationToken)
    {
        if (access is not null && await access.DefinitionOnFormAsync(formsRepository, (request.FormId, request.DefinitionId), cancellationToken) is { } refusal)
        {
            return refusal;
        }

        return await ExecuteAsync(request, cancellationToken);
    }

    private async Task<Result<FormDefinition>> ExecuteAsync(GetFormDefinitionByIdQuery request, CancellationToken cancellationToken)
    {
        var specification = new DefinitionByFormAndDefinitionIdSpec(request.FormId, request.DefinitionId);
        var formDefinition = await formsRepository.SingleOrDefaultAsync(specification, cancellationToken);
        if (formDefinition == null || formDefinition.FormId != request.FormId)
        {
            return Result.NotFound("Form definition not found.");
        }

        return Result.Success(formDefinition);
    }
}
