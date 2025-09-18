using Ardalis.GuardClauses;
using Endatix.Core.Entities;
using Endatix.Core.Infrastructure.Messaging;
using Endatix.Core.Infrastructure.Result;

namespace Endatix.Core.UseCases.Submissions.Delete;

/// <summary>
/// Command for deleting a submission.
/// </summary>
public record DeleteSubmissionCommand : ICommand<Result<Submission>>
{
    public long FormId { get; init; }
    public long SubmissionId { get; init; }

    public DeleteSubmissionCommand(long formId, long submissionId)
    {
        Guard.Against.NegativeOrZero(formId);
        Guard.Against.NegativeOrZero(submissionId);
        FormId = formId;
        SubmissionId = submissionId;
    }
}