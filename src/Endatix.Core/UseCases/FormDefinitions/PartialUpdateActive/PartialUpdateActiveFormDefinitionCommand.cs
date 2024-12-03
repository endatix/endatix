using Ardalis.GuardClauses;
using Endatix.Core.Entities;
using Endatix.Core.Infrastructure.Messaging;
using Endatix.Core.Infrastructure.Result;

namespace Endatix.Core.UseCases.FormDefinitions.PartialUpdateActive;

/// <summary>
/// Command for partially updating the active form definition.
/// </summary>
public record PartialUpdateActiveFormDefinitionCommand : ICommand<Result<FormDefinition>>
{
    public long FormId { get; init; }
    public bool? IsDraft { get; init; }
    public string? JsonData { get; init; }

    public PartialUpdateActiveFormDefinitionCommand(long formId, bool? isDraft, string? jsonData)
    {
        Guard.Against.NegativeOrZero(formId);

        FormId = formId;
        IsDraft = isDraft;
        JsonData = jsonData;
    }
}
