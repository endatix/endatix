using Endatix.Core.Entities;
using Endatix.Core.Infrastructure.Domain;
using Endatix.Core.Infrastructure.Result;
using Endatix.Core.Specifications;

namespace Endatix.Core.UseCases.Submissions;

/// <summary>
/// The 409 for a second real submission from one submitter on a one-per-person form. It names the existing
/// submission (<c>submission_exists</c>, <c>submissionId</c>) so the caller can resume it.
/// </summary>
internal static class SingleSubmissionConflict
{
    public const string MESSAGE = "A submission already exists for this user and form.";
    private const string SUBMISSION_EXISTS_CODE = "submission_exists";

    public static async Task<Result<T>> ResultAsync<T>(
        IRepository<Submission> submissions,
        SubmissionByFormIdAndSubmitterIdSpec spec,
        CancellationToken cancellationToken)
    {
        var existing = await submissions.FirstOrDefaultAsync(spec, cancellationToken);
        return existing is null
            ? Result<T>.Conflict(MESSAGE)
            : Result<T>.Conflict(
                new ExistingResource(SUBMISSION_EXISTS_CODE, "submissionId", existing.Id.ToString()),
                MESSAGE);
    }
}
