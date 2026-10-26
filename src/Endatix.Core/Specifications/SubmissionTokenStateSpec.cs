using Ardalis.Specification;
using Endatix.Core.Entities;

namespace Endatix.Core.Specifications;

/// <summary>What the after-completion rule for a respondent's token reads from a submission.</summary>
public sealed record SubmissionTokenState(long TenantId, bool IsComplete, CollectionStatus CollectionStatus)
{
    /// <summary>A screened-out interview has ended too, so its token follows the same rule.</summary>
    public bool HasEnded => IsComplete || CollectionStatus.Code == CollectionStatusCodes.ScreenOut;
}

/// <summary>A live submission's <see cref="SubmissionTokenState"/>, untracked.</summary>
public sealed class SubmissionTokenStateSpec : SingleResultSpecification<Submission, SubmissionTokenState>
{
    /// <summary>The submission by its id.</summary>
    public SubmissionTokenStateSpec(long submissionId) =>
        Query.Where(submission => submission.Id == submissionId)
            .AsNoTracking()
            .Select(submission => new SubmissionTokenState(
                submission.TenantId,
                submission.IsComplete,
                submission.CollectionStatus));
}
