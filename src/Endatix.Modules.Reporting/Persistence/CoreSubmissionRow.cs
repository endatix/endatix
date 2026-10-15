using Endatix.Core.Abstractions;

namespace Endatix.Modules.Reporting.Persistence;

/// <summary>SPIKE: read-only keyless view over the core Submissions table.</summary>
public sealed class CoreSubmissionRow : ITenantOwned
{
    public long Id { get; init; }
    public long TenantId { get; init; }
    public long FormId { get; init; }
    public bool IsDeleted { get; init; }
    public bool IsComplete { get; init; }
    public bool IsTestSubmission { get; init; }
    public string CollectionStatus { get; init; } = string.Empty;
    public DateTime CreatedAt { get; init; }
    public DateTime? ModifiedAt { get; init; }
    public DateTime? CompletedAt { get; init; }
    public string? SubmitterProfileSnapshot { get; init; }
}
