namespace Endatix.Core.Entities;

/// <summary>
/// Unified creation arguments for <see cref="Submission"/>. Named properties reduce the risk of
/// swapping <c>FormId</c> and <c>FormDefinitionId</c> at call sites.
/// </summary>
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
    bool EnforceSingleSubmissionGate = false);
