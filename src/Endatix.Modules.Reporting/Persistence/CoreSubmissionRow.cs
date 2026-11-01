using Endatix.Core.Abstractions;

namespace Endatix.Modules.Reporting.Persistence;

/// <summary>
/// A read-only view of the core <c>Submissions</c> table, so one reporting query can join a submission to its
/// prepared row. Only the columns the submissions grid filters, sorts and returns are mapped.
/// </summary>
/// <remarks>
/// The table belongs to the core context: it is excluded from Reporting migrations, and its column names follow the
/// core <c>SubmissionConfiguration</c>. Property names match <c>Submission</c>'s, so the core list filters, which
/// address fields by name, read the same columns here.
/// </remarks>
public sealed class CoreSubmissionRow : ITenantOwned
{
    /// <summary>The submission id.</summary>
    public long Id { get; init; }

    /// <summary>The tenant that owns the submission.</summary>
    public long TenantId { get; init; }

    /// <summary>The submission's form.</summary>
    public long FormId { get; init; }

    /// <summary>The form definition the submission was made with.</summary>
    public long FormDefinitionId { get; init; }

    /// <summary>Whether the submission is soft-deleted.</summary>
    public bool IsDeleted { get; init; }

    /// <summary>Whether the submission is complete.</summary>
    public bool IsComplete { get; init; }

    /// <summary>Whether the submission is a test submission.</summary>
    public bool IsTestSubmission { get; init; }

    /// <summary>The page the respondent is on.</summary>
    public int? CurrentPage { get; init; }

    /// <summary>The submission's answers as JSON.</summary>
    public string JsonData { get; init; } = string.Empty;

    /// <summary>The submission's metadata as JSON.</summary>
    public string? Metadata { get; init; }

    /// <summary>The <c>Status</c> column: the submission status code.</summary>
    public string Status { get; init; } = string.Empty;

    /// <summary>The <c>CollectionStatus</c> column: the collection status code.</summary>
    public string CollectionStatus { get; init; } = string.Empty;

    /// <summary>Who submitted it, when known.</summary>
    public string? SubmittedBy { get; init; }

    /// <summary>The submitter's id, when known.</summary>
    public long? SubmitterId { get; init; }

    /// <summary>The submitter's display id, when known.</summary>
    public string? SubmitterDisplayId { get; init; }

    /// <summary>The submitter's profile as JSON, as it was when they submitted.</summary>
    public string? SubmitterProfileSnapshot { get; init; }

    /// <summary>When the submission was created.</summary>
    public DateTime CreatedAt { get; init; }

    /// <summary>When the submission was last changed.</summary>
    public DateTime? ModifiedAt { get; init; }

    /// <summary>When the respondent started.</summary>
    public DateTime? StartedAt { get; init; }

    /// <summary>When the submission was completed.</summary>
    public DateTime? CompletedAt { get; init; }
}
