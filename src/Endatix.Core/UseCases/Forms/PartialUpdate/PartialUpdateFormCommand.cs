using Ardalis.GuardClauses;
using Endatix.Core.Entities;
using Endatix.Core.Infrastructure.Messaging;
using Endatix.Core.Infrastructure.Result;

namespace Endatix.Core.UseCases.Forms.PartialUpdate;

/// <summary>
/// Command for partially updating a form.
/// </summary>
public record PartialUpdateFormCommand : ICommand<Result<Form>>
{
    public long FormId { get; init; }
    public string? Name { get; init; }
    public string? Description { get; init; }
    public bool? IsEnabled { get; init; }
    public long? ThemeId { get; init; }
    public string? WebHookSettingsJson { get; init; }

    public PartialUpdateFormCommand(long formId, string? name, string? description, bool? isEnabled, long? themeId, string? webHookSettingsJson = null)
    {
        Guard.Against.NegativeOrZero(formId);

        FormId = formId;
        Name = name;
        Description = description;
        IsEnabled = isEnabled;
        ThemeId = themeId;
        WebHookSettingsJson = webHookSettingsJson;
    }
}
