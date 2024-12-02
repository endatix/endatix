using Endatix.Core.Entities;

namespace Endatix.Api.Endpoints.Submissions;

public class SubmissionMapper
{
    public static T Map<T>(Submission submission) where T : SubmissionModel, new() => new T
    {
        Id = submission.Id.ToString(),
        IsComplete = submission.IsComplete,
        JsonData = submission.JsonData,
        FormDefinitionId = submission.FormDefinitionId.ToString(),
        CurrentPage = submission.CurrentPage,
        Metadata = submission.Metadata,
        Token = submission.Token?.Value,
        CompletedAt = submission.CompletedAt,
        CreatedAt = submission.CreatedAt,
        ModifiedAt = submission.ModifiedAt
    };
    
    public static IEnumerable<T> Map<T>(IEnumerable<Submission> submissions) where T : SubmissionModel, new() =>
        submissions.Select(Map<T>).ToList();
}
