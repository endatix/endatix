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
    public bool? LimitOnePerUser { get; init; }
    public int? SubmissionTokenExpiryHours { get; init; }
    public bool ClearSubmissionTokenExpiryHours { get; init; }
    public string? Metadata { get; init; }
    public string? WebHookSettingsJson { get; init; }
    public long? FolderId { get; init; }

    public UpdateFormCommand(
        long formId,
        string name,
        string? description,
        bool isEnabled,
        string? webHookSettingsJson = null,
        bool? limitOnePerUser = null,
        int? submissionTokenExpiryHours = null,
        bool clearSubmissionTokenExpiryHours = false,
        string? metadata = null,
        long? folderId = null)
    {
        Guard.Against.NegativeOrZero(formId);
        Guard.Against.NullOrWhiteSpace(name);
        if (submissionTokenExpiryHours.HasValue)
        {
            Guard.Against.NegativeOrZero(submissionTokenExpiryHours.Value, nameof(submissionTokenExpiryHours));
        }

        FormId = formId;
        Name = name;
        Description = description;
        IsEnabled = isEnabled;
        LimitOnePerUser = limitOnePerUser;
        SubmissionTokenExpiryHours = submissionTokenExpiryHours;
        ClearSubmissionTokenExpiryHours = clearSubmissionTokenExpiryHours;
        Metadata = metadata;
        WebHookSettingsJson = webHookSettingsJson;
        FolderId = folderId;
    }
}
