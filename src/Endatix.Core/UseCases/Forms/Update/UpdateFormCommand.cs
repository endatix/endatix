using Ardalis.GuardClauses;
using Endatix.Core.Entities;
using Endatix.Core.Infrastructure.Messaging;
using Endatix.Core.Infrastructure.Result;

namespace Endatix.Core.UseCases.Forms.Update;

/// <summary>
/// Command for updating a form.
/// </summary>
public record UpdateFormCommand : ICommand<Result<Form>>
{
    public long FormId { get; init; }
    public string Name { get; init; }
    public string? Description { get; init; }
    public bool IsEnabled { get; init; }
    public string? WebHookSettingsJson { get; init; }

    public UpdateFormCommand(long formId, string name, string? description, bool isEnabled, string? webHookSettingsJson = null)
    {
        Guard.Against.NegativeOrZero(formId);
        Guard.Against.NullOrWhiteSpace(name);

        FormId = formId;
        Name = name;
        Description = description;
        IsEnabled = isEnabled;
        WebHookSettingsJson = webHookSettingsJson;
    }
}
