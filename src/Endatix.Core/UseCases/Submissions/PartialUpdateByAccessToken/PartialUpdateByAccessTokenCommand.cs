using Ardalis.GuardClauses;
using Endatix.Core.Entities;
using Endatix.Core.Infrastructure.Messaging;
using Endatix.Core.Infrastructure.Result;

namespace Endatix.Core.UseCases.Submissions.PartialUpdateByAccessToken;

/// <summary>
/// Command for partially updating a form submission by access token.
/// </summary>
public record PartialUpdateByAccessTokenCommand : ICommand<Result<Submission>>
{
    public string AccessToken { get; init; }
    public long FormId { get; init; }
    public bool? IsComplete { get; init; }
    public int? CurrentPage { get; init; }
    public string? JsonData { get; init; }
    public string? Metadata { get; init; }

    public PartialUpdateByAccessTokenCommand(
        string accessToken,
        long formId,
        bool? isComplete,
        int? currentPage,
        string? jsonData,
        string? metadata)
    {
        Guard.Against.NullOrEmpty(accessToken);
        Guard.Against.NegativeOrZero(formId);

        if (currentPage.HasValue)
        {
            Guard.Against.Negative(currentPage.Value);
        }

        AccessToken = accessToken;
        FormId = formId;
        IsComplete = isComplete;
        CurrentPage = currentPage;
        JsonData = jsonData;
        Metadata = metadata;
    }
}
