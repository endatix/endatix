using Ardalis.GuardClauses;
using Endatix.Core.Entities;
using Endatix.Core.Infrastructure.Messaging;
using Endatix.Core.Infrastructure.Result;

namespace Endatix.Core.UseCases.FormDefinitions.PartialUpdate;

/// <summary>
/// Command for partially updating a form definition.
/// </summary>
public record PartialUpdateFormDefinitionCommand : ICommand<Result<FormDefinition>>
{
    public long FormId { get; init; }
    public long DefinitionId { get; init; }
    public bool? IsDraft { get; init; }
    public string? JsonData { get; init; }

    public PartialUpdateFormDefinitionCommand(long formId, long definitionId, bool? isDraft, string? jsonData)
    {
        Guard.Against.NegativeOrZero(formId);
        Guard.Against.NegativeOrZero(definitionId);

        FormId = formId;
        DefinitionId = definitionId;
        IsDraft = isDraft;
        JsonData = jsonData;
    }
}
