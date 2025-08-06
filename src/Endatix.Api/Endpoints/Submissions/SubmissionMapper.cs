using Endatix.Core.Entities;

namespace Endatix.Api.Endpoints.Submissions;

public class SubmissionMapper
{
    public static T Map<T>(Submission submission) where T : SubmissionModel, new() => new T
    {
        Id = submission.Id.ToString(),
        IsComplete = submission.IsComplete,
        JsonData = submission.JsonData,
        FormId = submission.FormId.ToString(),
        FormDefinitionId = submission.FormDefinitionId.ToString(),
        CurrentPage = submission.CurrentPage,
        Metadata = submission.Metadata,
        Token = submission.Token?.Value,
        CompletedAt = submission.CompletedAt,
        CreatedAt = submission.CreatedAt,
        ModifiedAt = submission.ModifiedAt,
        Status = submission.Status.Code,
        SubmittedBy = submission.SubmittedBy?.ToString()
    };

    public static SubmissionDetailsModel MapToSubmissionDetails(Submission submission)
    {
        var mappedModel = Map<SubmissionDetailsModel>(submission);
        if (submission.FormDefinition is not null)
        {
            mappedModel.FormDefinition = new()
            {
                Id = submission.FormDefinition.Id.ToString(),
                IsDraft = submission.FormDefinition.IsDraft,
                JsonData = submission.FormDefinition.JsonData,
                CreatedAt = submission.FormDefinition.CreatedAt,
                ModifiedAt = submission.FormDefinition.ModifiedAt
            };
        }

        return mappedModel;
    }

    public static IEnumerable<T> Map<T>(IEnumerable<Submission> submissions) where T : SubmissionModel, new() =>
        submissions.Select(Map<T>).ToList();
}
