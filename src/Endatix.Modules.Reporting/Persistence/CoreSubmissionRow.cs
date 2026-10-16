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
    public long Id { get; init; }

    public long TenantId { get; init; }

    public long FormId { get; init; }

    public long FormDefinitionId { get; init; }

    public bool IsDeleted { get; init; }

    public bool IsComplete { get; init; }

    public bool IsTestSubmission { get; init; }

    public int? CurrentPage { get; init; }

    public string JsonData { get; init; } = string.Empty;

    public string? Metadata { get; init; }

    /// <summary>The <c>Status</c> column: the submission status code.</summary>
    public string Status { get; init; } = string.Empty;

    /// <summary>The <c>CollectionStatus</c> column: the collection status code.</summary>
    public string CollectionStatus { get; init; } = string.Empty;

    public string? SubmittedBy { get; init; }

    public long? SubmitterId { get; init; }

    public string? SubmitterDisplayId { get; init; }

    public string? SubmitterProfileSnapshot { get; init; }

    public DateTime CreatedAt { get; init; }

    public DateTime? ModifiedAt { get; init; }

    public DateTime? StartedAt { get; init; }

    public DateTime? CompletedAt { get; init; }
}
