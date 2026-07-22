namespace Endatix.Core.Entities;

/// <summary>
/// Unified creation arguments for <see cref="Submission"/>. Named properties reduce the risk of
/// swapping <c>FormId</c> and <c>FormDefinitionId</c> at call sites.
/// </summary>
/// <param name="StartSubmission">
/// When true, stamps <see cref="Submission.StartedAt"/> on create (respondent <c>Create</c> path).
/// Leave false for prefill / create-on-behalf so start remains null until first engagement update.
/// Complete-on-create still sets start via completion when this is false.
/// </param>
public sealed record SubmissionCreateArgs(
    long TenantId,
    long FormId,
    long FormDefinitionId,
    string JsonData,
    bool IsComplete = true,
    int? CurrentPage = null,
    string? Metadata = null,
    long? SubmitterId = null,
    string? SubmitterDisplayId = null,
    string? SubmitterProfileSnapshot = null,
    bool IsTestSubmission = false,
    bool EnforceSingleSubmissionGate = false,
    bool StartSubmission = false);
