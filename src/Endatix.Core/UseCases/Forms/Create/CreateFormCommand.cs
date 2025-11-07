using Ardalis.GuardClauses;
using Endatix.Core.Entities;
using Endatix.Core.Infrastructure.Messaging;
using Endatix.Core.Infrastructure.Result;

namespace Endatix.Core.UseCases.Forms.Create;

/// <summary>
/// Command for creating a form.
/// </summary>
public record CreateFormCommand : ICommand<Result<Form>>
{
    public string Name { get; init; }
    public string? Description { get; init; }
    public bool IsEnabled { get; init; }
    public string FormDefinitionJsonData { get; init; }
    public string? WebHookSettingsJson { get; init; }

    public CreateFormCommand(string name, string? description, bool isEnabled, string formDefinitionJsonData, string? webHookSettingsJson = null)
    {
        Guard.Against.NullOrWhiteSpace(name);
        Guard.Against.NullOrWhiteSpace(formDefinitionJsonData);

        Name = name;
        Description = description;
        IsEnabled = isEnabled;
        FormDefinitionJsonData = formDefinitionJsonData;
        WebHookSettingsJson = webHookSettingsJson;
    }
}
