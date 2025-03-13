using Ardalis.GuardClauses;
using Endatix.Core.Entities;
using Endatix.Core.Infrastructure.Messaging;
using Endatix.Core.Infrastructure.Result;

namespace Endatix.Core.UseCases.FormTemplates.Delete;

/// <summary>
/// Command for deleting a form template.
/// </summary>
public record DeleteFormTemplateCommand : ICommand<Result<FormTemplate>>
{
    public long FormTemplateId { get; init; }

    public DeleteFormTemplateCommand(long formTemplateId)
    {
        Guard.Against.NegativeOrZero(formTemplateId);
        FormTemplateId = formTemplateId;
    }
}
