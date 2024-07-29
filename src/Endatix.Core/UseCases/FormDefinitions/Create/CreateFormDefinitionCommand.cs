using Ardalis.GuardClauses;
using Endatix.Core.Entities;
using Endatix.Core.Infrastructure.Messaging;
using Endatix.Core.Infrastructure.Result;

namespace Endatix.Core.UseCases.FormDefinitions.Create;

/// <summary>
/// Command for creating a form definition.
/// </summary>
public record CreateFormDefinitionCommand : ICommand<Result<FormDefinition>>
{
    public long FormId { get; init; }
    public bool IsDraft { get; init; }
    public string JsonData { get; init; }
    public bool IsActive { get; init; }

    public CreateFormDefinitionCommand(long formId, bool isDraft, string jsonData, bool isActive)
    {
        Guard.Against.NegativeOrZero(formId);
        Guard.Against.NullOrWhiteSpace(jsonData);

        FormId = formId;
        IsDraft = isDraft;
        JsonData = jsonData;
        IsActive = isActive;
    }
}
