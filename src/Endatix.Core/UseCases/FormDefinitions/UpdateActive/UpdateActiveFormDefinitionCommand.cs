using Ardalis.GuardClauses;
using Endatix.Core.Entities;
using Endatix.Core.Infrastructure.Messaging;
using Endatix.Core.Infrastructure.Result;

namespace Endatix.Core.UseCases.FormDefinitions.UpdateActive;

/// <summary>
/// Command for updating the active form definition.
/// </summary>
public record UpdateActiveFormDefinitionCommand : ICommand<Result<FormDefinition>>
{
    public long FormId { get; init; }
    public bool IsDraft { get; init; }
    public string JsonData { get; init; }

    public UpdateActiveFormDefinitionCommand(long formId, bool isDraft, string jsonData)
    {
        Guard.Against.NegativeOrZero(formId);
        Guard.Against.NullOrWhiteSpace(jsonData);

        FormId = formId;
        IsDraft = isDraft;
        JsonData = jsonData;
    }
}
