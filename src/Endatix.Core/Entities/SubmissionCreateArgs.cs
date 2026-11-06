namespace Endatix.Core.Entities;

/// <summary>
/// Unified creation arguments for <see cref="Submission"/>. Named properties reduce the risk of
/// swapping <c>FormId</c> and <c>FormDefinitionId</c> at call sites.
/// </summary>
/// <param name="TenantId">Owning tenant.</param>
/// <param name="FormId">Form the submission belongs to.</param>
/// <param name="FormDefinitionId">Form definition version used for the submission.</param>
/// <param name="JsonData">Submission answer payload JSON.</param>
/// <param name="IsComplete">Whether the submission is complete on create.</param>
/// <param name="CurrentPage">Current page index; optional. Stored as given, also for a complete submission.</param>
/// <param name="Metadata">Optional metadata JSON.</param>
/// <param name="SubmitterId">Optional linked submitter id.</param>
/// <param name="SubmitterDisplayId">Optional display id for the submitter.</param>
/// <param name="SubmitterProfileSnapshot">Optional frozen submitter profile JSON.</param>
/// <param name="IsTestSubmission">Marks the submission as a test submission.</param>
/// <param name="EnforceSingleSubmissionGate">
/// When true, enforces single-submission rules on create. Ignored for a test submission.
/// </param>
/// <param name="StartSubmission">
/// When true, stamps <see cref="Submission.StartedAt"/> on create (respondent <c>Create</c> path).
/// Leave false for prefill / create-on-behalf so start remains null until first engagement update.
/// Complete-on-create still sets start via completion when this is false.
/// </param>
/// <param name="CollectionOutcome">
/// A closed-phase status the submission is created in, e.g. <see cref="CollectionStatus.ScreenOut"/>. Raises
/// <c>submission.created</c> carrying it, then <c>submission.closed</c>. Not combined with <paramref name="IsComplete"/>.
/// </param>
/// <param name="OpenSubmission">
/// When true, the respondent's open created the submission: it is saved <c>viewed</c> with
/// <see cref="Submission.StartedAt"/> stamped. Not combined with <paramref name="StartSubmission"/>.
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
    bool StartSubmission = false,
    CollectionStatus? CollectionOutcome = null,
    bool OpenSubmission = false);
