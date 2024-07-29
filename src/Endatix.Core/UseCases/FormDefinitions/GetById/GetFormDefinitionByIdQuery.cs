using Ardalis.GuardClauses;
using Endatix.Core.Entities;
using Endatix.Core.Infrastructure.Messaging;
using Endatix.Core.Infrastructure.Result;

namespace Endatix.Core.UseCases.FormDefinitions.GetById;

/// <summary>
/// Query for getting a form definition by ID.
/// </summary>
public record GetFormDefinitionByIdQuery : IQuery<Result<FormDefinition>>
{
    public long FormId { get; init; }
    public long DefinitionId { get; init; }

    public GetFormDefinitionByIdQuery(long formId, long definitionId)
    {
        Guard.Against.NegativeOrZero(formId);
        Guard.Against.NegativeOrZero(definitionId);

        FormId = formId;
        DefinitionId = definitionId;
    }
}
