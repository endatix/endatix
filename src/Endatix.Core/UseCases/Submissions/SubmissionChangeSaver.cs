using Endatix.Core.Abstractions.Repositories;
using Endatix.Core.Entities;
using Endatix.Core.Infrastructure.Domain;
using Endatix.Core.Infrastructure.Result;
using Endatix.Core.Specifications;

namespace Endatix.Core.UseCases.Submissions;

/// <summary>
/// Saves a change to a submission's answers or state, with a version of the answers it replaced.
/// </summary>
internal sealed class SubmissionChangeSaver(IRepository<Submission> submissions, IRepository<SubmissionVersion> versions, ContentQueryScope scope)
{
    internal Task<Submission?> FindAsync(long formId, long submissionId, CancellationToken ct) =>
        submissions.SingleOrDefaultAsync(scope.Submission(new SubmissionByFormIdAndSubmissionIdSpec(formId, submissionId)), ct);

    internal Task<Submission?> FindForUpdateAsync(PartialUpdate.PartialUpdateSubmissionCommand request, CancellationToken ct)
    {
        var spec = new SubmissionByFormIdAndSubmissionIdSpec(request.FormId, request.SubmissionId);
        return submissions.SingleOrDefaultAsync(request.TokenTenantId is long id ? spec.OnLiveForm(id) : scope.Submission(spec), ct);
    }

    /// <summary>
    /// Reports as <see cref="SubmissionRepositoryExtensions.TrySaveChangesAsync"/> does. A refused save rolled back,
    /// the version and any event with it.
    /// </summary>
    public Task<Result> TrySaveAsync(Submission submission, string originalJson, CancellationToken cancellationToken) =>
        SubmissionRepositoryExtensions.SaveAsync(() => SaveAsync(submission, originalJson, cancellationToken));

    /// <summary>Adding the version saves it with the change, so either call can find the submission deleted.</summary>
    private async Task SaveAsync(Submission submission, string originalJson, CancellationToken cancellationToken)
    {
        if (!string.Equals(originalJson, submission.JsonData, StringComparison.Ordinal))
        {
            var effectiveTimestamp = submission.ModifiedAt ?? submission.CreatedAt;
            await versions.AddAsync(new SubmissionVersion(submission.Id, originalJson, effectiveTimestamp), cancellationToken);
        }

        await submissions.SaveChangesAsync(cancellationToken);
    }
}
