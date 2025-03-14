using Ardalis.GuardClauses;
using Endatix.Core.Entities;
using Endatix.Core.Infrastructure.Messaging;
using Endatix.Core.Infrastructure.Result;

namespace Endatix.Core.UseCases.FormTemplates.PartialUpdate;

/// <summary>
/// Command for partially updating a form template.
/// </summary>
public record PartialUpdateFormTemplateCommand : ICommand<Result<FormTemplate>>
{
    public long FormTemplateId { get; init; }
    public string? Name { get; init; }
    public string? Description { get; init; }
    public string? JsonData { get; init; }
    public bool? IsEnabled { get; init; }

    public PartialUpdateFormTemplateCommand(long formTemplateId, string? name, string? description, string? jsonData, bool? isEnabled)
    {
        Guard.Against.NegativeOrZero(formTemplateId);

        FormTemplateId = formTemplateId;
        Name = name;
        Description = description;
        JsonData = jsonData;
        IsEnabled = isEnabled;
    }
}
