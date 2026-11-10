using System.Text.Json;
using Endatix.Core.Entities;
using Endatix.Core.UseCases.Submissions;

namespace Endatix.Api.Endpoints.Submissions;

public static class SubmissionMapper
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
        StartedAt = submission.StartedAt,
        CreatedAt = submission.CreatedAt,
        ModifiedAt = submission.ModifiedAt,
        Status = submission.Status.Code,
        SubmittedBy = submission.SubmittedBy,
        SubmitterId = submission.SubmitterId?.ToString(),
        SubmitterDisplayId = submission.SubmitterDisplayId,
        SubmitterProfile = ParseSubmitterProfile(submission.SubmitterProfileSnapshot),
        IsTestSubmission = submission.IsTestSubmission,
        CollectionStatus = submission.CollectionStatus.Code,
        Personalization = PersonalizationReader.Read(submission.PersonalizationSnapshot, includeValues: false),
    };

    /// <summary>Maps a collection of submission entities to API models.</summary>
    public static IEnumerable<T> Map<T>(IEnumerable<Submission> submissions) where T : SubmissionModel, new() =>
        submissions.Select(Map<T>).ToList();

    /// <summary>Maps a submission DTO to the API model.</summary>
    public static SubmissionModel MapFromDto(SubmissionDto dto) => new SubmissionModel
    {
        Id = dto.Id.ToString(),
        IsComplete = dto.IsComplete,
        JsonData = dto.JsonData,
        FormId = dto.FormId.ToString(),
        FormDefinitionId = dto.FormDefinitionId.ToString(),
        CurrentPage = dto.CurrentPage,
        CompletedAt = dto.CompletedAt,
        StartedAt = dto.StartedAt,
        CreatedAt = dto.CreatedAt,
        ModifiedAt = dto.ModifiedAt,
        Metadata = dto.Metadata,
        Status = dto.Status,
        SubmittedBy = dto.SubmittedBy,
        SubmitterId = dto.SubmitterId?.ToString(),
        SubmitterDisplayId = dto.SubmitterDisplayId,
        SubmitterProfile = dto.SubmitterProfile,
        IsTestSubmission = dto.IsTestSubmission,
        CollectionStatus = dto.CollectionStatus
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

    private static IReadOnlyDictionary<string, string>? ParseSubmitterProfile(string? snapshot)
    {
        if (string.IsNullOrWhiteSpace(snapshot))
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<Dictionary<string, string>>(snapshot);
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
