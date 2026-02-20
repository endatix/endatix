using Ardalis.GuardClauses;
using Endatix.Core.Infrastructure.Messaging;
using Endatix.Core.Infrastructure.Result;

namespace Endatix.Core.UseCases.FormDefinitions.GetFields;

/// <summary>
/// Query to retrieve the union of all fields across all definitions for a form.
/// </summary>
public record GetFormDefinitionFieldsQuery : IQuery<Result<IEnumerable<DefinitionFieldDto>>>
{
    public long FormId { get; init; }

    public GetFormDefinitionFieldsQuery(long formId)
    {
        Guard.Against.NegativeOrZero(formId);
        FormId = formId;
    }
}
