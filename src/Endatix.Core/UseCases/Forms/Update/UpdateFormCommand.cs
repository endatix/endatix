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
        UpdateFormOptions options = default)
    {
        Guard.Against.NegativeOrZero(formId);
        Guard.Against.NullOrWhiteSpace(name);
        if (options.SubmissionTokenExpiryHours.HasValue)
        {
            Guard.Against.NegativeOrZero(options.SubmissionTokenExpiryHours.Value);
        }

        FormId = formId;
        Name = name;
        Description = description;
        IsEnabled = isEnabled;
        LimitOnePerUser = options.LimitOnePerUser;
        SubmissionTokenExpiryHours = options.SubmissionTokenExpiryHours;
        ClearSubmissionTokenExpiryHours = options.ClearSubmissionTokenExpiryHours;
        Metadata = options.Metadata;
        WebHookSettingsJson = webHookSettingsJson;
        FolderId = options.FolderId;
    }
}

/// <summary>
/// Optional values for <see cref="UpdateFormCommand"/>. Defaults match the previous constructor.
/// </summary>
public readonly record struct UpdateFormOptions(
    bool? LimitOnePerUser = null,
    int? SubmissionTokenExpiryHours = null,
    bool ClearSubmissionTokenExpiryHours = false,
    string? Metadata = null,
    long? FolderId = null);
