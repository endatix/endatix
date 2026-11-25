using Endatix.Core.Abstractions;
using Endatix.Core.Abstractions.Repositories;
using Endatix.Core.Entities;
using Endatix.Core.Infrastructure.Messaging;
using Endatix.Core.Infrastructure.Result;

namespace Endatix.Core.UseCases.FormDefinitions.GetById;

/// <summary>Reads a definition belonging to the requested form and tenant.</summary>
[DirectCallOnly]
public class GetFormDefinitionByIdHandler(IFormsRepository formsRepository) : IQueryHandler<GetFormDefinitionByIdQuery, Result<FormDefinition>>
{
    private ContentQueryScope Selection { get; init; } = ContentQueryScope.Repository;

    public async Task<Result<FormDefinition>> Handle(GetFormDefinitionByIdQuery request, CancellationToken cancellationToken)
    {
        var specification = new DefinitionByFormAndDefinitionIdSpec(request.FormId, request.DefinitionId, Selection.TenantId);
        var formDefinition = await formsRepository.SingleOrDefaultAsync(specification, cancellationToken);
        if (formDefinition == null || formDefinition.FormId != request.FormId)
        {
            return Result.NotFound("Form definition not found.");
        }

        return Result.Success(formDefinition);
    }

    internal sealed class Registered(IFormsRepository formsRepository, ContentQueryScope tenant)
        : RegisteredContentHandler<GetFormDefinitionByIdQuery, Result<FormDefinition>>(
            new GetFormDefinitionByIdHandler(formsRepository) { Selection = tenant });
}
