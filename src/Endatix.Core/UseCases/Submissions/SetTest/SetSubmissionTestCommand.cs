using Ardalis.GuardClauses;
using Endatix.Core.Infrastructure.Messaging;
using Endatix.Core.Infrastructure.Result;

namespace Endatix.Core.UseCases.Submissions.SetTest;

/// <summary>Marks a submission as test (<paramref name="IsTest"/> true) or as real.</summary>
public sealed record SetSubmissionTestCommand : ICommand<Result<SubmissionDto>>
{
    public SetSubmissionTestCommand(long formId, long submissionId, bool isTest)
    {
        Guard.Against.NegativeOrZero(formId);
        Guard.Against.NegativeOrZero(submissionId);
        FormId = formId;
        SubmissionId = submissionId;
        IsTest = isTest;
    }

    public long FormId { get; }
    public long SubmissionId { get; }
    public bool IsTest { get; }
}
