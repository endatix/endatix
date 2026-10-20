using Ardalis.GuardClauses;
using Endatix.Core.Infrastructure.Messaging;
using Endatix.Core.Infrastructure.Result;

namespace Endatix.Core.UseCases.Submissions.DeleteTests;

/// <summary>Soft-deletes every test submission of a form.</summary>
public sealed record DeleteTestSubmissionsCommand : ICommand<Result<DeletedTestSubmissions>>
{
    /// <summary>Deletes the test submissions of <paramref name="formId"/>.</summary>
    /// <param name="tenantId">The caller's tenant. The handler refuses a value of 0 or less.</param>
    /// <param name="formId">The form.</param>
    public DeleteTestSubmissionsCommand(long tenantId, long formId)
    {
        Guard.Against.NegativeOrZero(formId);
        TenantId = tenantId;
        FormId = formId;
    }

    /// <summary>The caller's tenant.</summary>
    public long TenantId { get; }

    /// <summary>The form whose test submissions are deleted.</summary>
    public long FormId { get; }
}

/// <summary>How many test submissions were deleted.</summary>
public sealed record DeletedTestSubmissions(int Count);
