using Endatix.Core.Entities;
using Endatix.Core.UseCases.Submissions;
using System.Text.Json;

namespace Endatix.Api.Endpoints.Submissions;

public class SubmissionMapper
{
    public static SubmissionModel MapFromDto(SubmissionDto dto) => new SubmissionModel
    {
        Id = dto.Id.ToString(),
        IsComplete = dto.IsComplete,
        JsonData = dto.JsonData,
        FormId = dto.FormId.ToString(),
        FormDefinitionId = dto.FormDefinitionId.ToString(),
        CurrentPage = dto.CurrentPage,
        CompletedAt = dto.CompletedAt,
        CreatedAt = dto.CreatedAt,
        Metadata = dto.Metadata,
        Status = dto.Status,
        SubmittedBy = dto.SubmittedBy,
        SubmitterId = dto.SubmitterId?.ToString(),
        SubmitterDisplayId = dto.SubmitterDisplayId,
        SubmitterProfile = dto.SubmitterProfile,
        IsTestSubmission = dto.IsTestSubmission
    };


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
        SubmittedBy = submission.SubmittedBy,
        SubmitterId = submission.SubmitterId?.ToString(),
        SubmitterDisplayId = submission.SubmitterDisplayId,
        SubmitterProfile = ParseSubmitterProfile(submission.SubmitterProfileSnapshot),
        IsTestSubmission = submission.IsTestSubmission
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

    private static IReadOnlyDictionary<string, string>? ParseSubmitterProfile(string? snapshot)
    {
        if (string.IsNullOrWhiteSpace(snapshot))
        {
            return null;
        }

        return JsonSerializer.Deserialize<Dictionary<string, string>>(snapshot);
    }
}
