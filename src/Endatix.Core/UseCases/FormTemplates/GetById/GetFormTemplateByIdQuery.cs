using Ardalis.GuardClauses;
using Endatix.Core.Entities;
using Endatix.Core.Infrastructure.Messaging;
using Endatix.Core.Infrastructure.Result;

namespace Endatix.Core.UseCases.FormTemplates.GetById;

/// <summary>
/// Query for getting a form template by ID.
/// </summary>
public record GetFormTemplateByIdQuery : IQuery<Result<FormTemplate>>
{
    public long FormTemplateId { get; init; }

    public GetFormTemplateByIdQuery(long formTemplateId)
    {
        Guard.Against.NegativeOrZero(formTemplateId);
        FormTemplateId = formTemplateId;
    }
}
