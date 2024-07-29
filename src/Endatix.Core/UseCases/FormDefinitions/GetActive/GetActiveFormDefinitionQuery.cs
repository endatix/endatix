using Ardalis.GuardClauses;
using Endatix.Core.Entities;
using Endatix.Core.Infrastructure.Messaging;
using Endatix.Core.Infrastructure.Result;

namespace Endatix.Core.UseCases.FormDefinitions.GetActive;

/// <summary>
/// Query for getting the active form definition.
/// </summary>
public record GetActiveFormDefinitionQuery : IQuery<Result<FormDefinition>>
{
    public long FormId { get; init; }

    public GetActiveFormDefinitionQuery(long formId)
    {
        Guard.Against.NegativeOrZero(formId);
        FormId = formId;
    }
}
