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
    public bool LimitOnePerUser { get; init; }
    public int? SubmissionTokenExpiryHours { get; init; }
    public string? Metadata { get; init; }
    public string FormDefinitionJsonData { get; init; }
    public string? WebHookSettingsJson { get; init; }
    public long? FolderId { get; init; }

    public CreateFormCommand(
        string name,
        string? description,
        bool isEnabled,
        string formDefinitionJsonData,
        string? webHookSettingsJson = null,
        CreateFormOptions options = default)
    {
        Guard.Against.NullOrWhiteSpace(name);
        Guard.Against.NullOrWhiteSpace(formDefinitionJsonData);
        SubmissionGuards.AgainstInvalidSubmissionTokenExpiry(options.SubmissionTokenExpiryHours);

        Name = name;
        Description = description;
        IsEnabled = isEnabled;
        LimitOnePerUser = options.LimitOnePerUser;
        SubmissionTokenExpiryHours = options.SubmissionTokenExpiryHours;
        Metadata = options.Metadata;
        FormDefinitionJsonData = formDefinitionJsonData;
        WebHookSettingsJson = webHookSettingsJson;
        FolderId = options.FolderId;
    }
}

/// <summary>
/// Optional values for <see cref="CreateFormCommand"/>. Defaults match the previous constructor.
/// </summary>
public readonly record struct CreateFormOptions(
    bool LimitOnePerUser = false,
    int? SubmissionTokenExpiryHours = null,
    string? Metadata = null,
    long? FolderId = null);
