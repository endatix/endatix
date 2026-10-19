using Ardalis.GuardClauses;
using Endatix.Core.Infrastructure.Messaging;
using Endatix.Core.Infrastructure.Result;

namespace Endatix.Core.UseCases.Submissions.DeleteTests;

/// <summary>Soft-deletes every test submission of a form.</summary>
public sealed record DeleteTestSubmissionsCommand : ICommand<Result<DeletedTestSubmissions>>
{
    /// <summary>Deletes the test submissions of <paramref name="formId"/>.</summary>
    /// <param name="formId">The form.</param>
    public DeleteTestSubmissionsCommand(long formId)
    {
        Guard.Against.NegativeOrZero(formId);
        FormId = formId;
    }

    /// <summary>The form whose test submissions are deleted.</summary>
    public long FormId { get; }
}

/// <summary>How many test submissions were deleted.</summary>
public sealed record DeletedTestSubmissions(int Count);
