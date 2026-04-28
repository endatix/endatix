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
    private long _formId;
    private long? _themeId;

    public long FormId
    {
        get => _formId;
        init => _formId = Guard.Against.NegativeOrZero(value, nameof(FormId));
    }

    public string? Name { get; init; }
    public string? Description { get; init; }
    public bool? IsEnabled { get; init; }
    public bool? IsPublic { get; init; }
    public bool? LimitOnePerUser { get; init; }
    public string? Metadata { get; init; }
    public long? ThemeId
    {
        get => _themeId;
        init
        {
            if (value.HasValue)
            {
                Guard.Against.Negative(value.Value, nameof(ThemeId));
            }

            _themeId = value;
        }
    }

    public string? WebHookSettingsJson { get; init; }

    public PartialUpdateFormCommand(long formId)
    {
        FormId = formId;
    }
}
