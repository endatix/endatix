using Ardalis.GuardClauses;
using Endatix.Core.Entities;
using Endatix.Core.Infrastructure.Messaging;
using Endatix.Core.Infrastructure.Result;

namespace Endatix.Core.UseCases.Submissions;

/// <summary>
/// Command for partially updating a form submission by token.
/// </summary>
public record PartialUpdateSubmissionByTokenCommand : ICommand<Result<Submission>>
{
    public string Token { get; init; }
    public long FormId { get; init; }
    public bool? IsComplete { get; init; }
    public int? CurrentPage { get; init; }
    public string? JsonData { get; init; }
    public string? Metadata { get; init; }

    public PartialUpdateSubmissionByTokenCommand(string token, long formId, bool? isComplete, int? currentPage, string? jsonData, string? metadata)
    {
        Guard.Against.NullOrEmpty(token);
        Guard.Against.NegativeOrZero(formId);

        Token = token;
        FormId = formId;
        IsComplete = isComplete;
        CurrentPage = currentPage;
        JsonData = jsonData;
        Metadata = metadata;
    }
}
