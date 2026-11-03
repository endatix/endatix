using Endatix.Core.Entities;

namespace Endatix.Core.Events;

/// <summary>
/// The submission state a submission event keeps from the moment it is raised, so a payload built later in the
/// same transaction still reports it.
/// </summary>
public readonly record struct SubmissionEventCapture(long Revision, bool IsTestSubmission)
{
    /// <summary>Captures the submission's current revision and test flag.</summary>
    public static SubmissionEventCapture Of(Submission submission) =>
        new(submission.Revision, submission.IsTestSubmission);
}
