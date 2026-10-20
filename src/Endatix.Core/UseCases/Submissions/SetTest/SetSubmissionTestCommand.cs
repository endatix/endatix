using Ardalis.GuardClauses;
using Endatix.Core.Infrastructure.Messaging;
using Endatix.Core.Infrastructure.Result;

namespace Endatix.Core.UseCases.Submissions.SetTest;

/// <summary>Marks a submission as test (<see cref="IsTest"/> true) or as real.</summary>
public sealed record SetSubmissionTestCommand : ICommand<Result<SubmissionDto>>
{
    /// <summary>Marks <paramref name="submissionId"/> on <paramref name="formId"/>.</summary>
    /// <param name="tenantId">The caller's tenant. The handler refuses a value of 0 or less.</param>
    /// <param name="formId">The form the submission belongs to.</param>
    /// <param name="submissionId">The submission to mark.</param>
    /// <param name="isTest"><see langword="true"/> for test, <see langword="false"/> for real.</param>
    public SetSubmissionTestCommand(long tenantId, long formId, long submissionId, bool isTest)
    {
        Guard.Against.NegativeOrZero(formId);
        Guard.Against.NegativeOrZero(submissionId);
        TenantId = tenantId;
        FormId = formId;
        SubmissionId = submissionId;
        IsTest = isTest;
    }

    /// <summary>The caller's tenant.</summary>
    public long TenantId { get; }

    /// <summary>The form the submission belongs to.</summary>
    public long FormId { get; }

    /// <summary>The submission to mark.</summary>
    public long SubmissionId { get; }

    /// <summary><see langword="true"/> marks the submission as test, <see langword="false"/> as real.</summary>
    public bool IsTest { get; }
}
