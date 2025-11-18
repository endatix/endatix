using Endatix.Core.Infrastructure.Domain;

namespace Endatix.Core.Entities;

/// <summary>
/// Represents a version/snapshot of a submission's JSON data at a specific point in time.
/// This entity tracks the history of JSON data changes to prevent data loss.
/// </summary>
public class SubmissionVersion : BaseEntity, IAggregateRoot
{
    private SubmissionVersion() { } // For EF Core

    public SubmissionVersion(long submissionId, string jsonData, DateTime createdAt)
    {
        SubmissionId = submissionId;
        JsonData = jsonData;
        CreatedAt = createdAt;
    }

    /// <summary>
    /// The ID of the submission this version represents
    /// </summary>
    public long SubmissionId { get; private set; }

    /// <summary>
    /// The JSON data at the time this version was created
    /// </summary>
    public string JsonData { get; private set; } = null!;



    /// <summary>
    /// Navigation property to the submission
    /// </summary>
    public Submission Submission { get; private set; } = null!;
}
