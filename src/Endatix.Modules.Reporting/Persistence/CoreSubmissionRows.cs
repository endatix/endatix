using Endatix.Core.UseCases.Submissions;

namespace Endatix.Modules.Reporting.Persistence;

/// <summary>Maps <see cref="CoreSubmissionRow"/> to the list's submission shape.</summary>
internal static class CoreSubmissionRows
{
    public static SubmissionDto ToDto(CoreSubmissionRow row) =>
        new(
            row.Id,
            row.IsComplete,
            row.JsonData,
            row.FormId,
            row.FormDefinitionId,
            row.CurrentPage,
            row.CompletedAt,
            row.StartedAt,
            row.CreatedAt,
            row.ModifiedAt,
            row.Metadata,
            row.Status,
            row.SubmittedBy,
            row.SubmitterId,
            row.SubmitterDisplayId,
            row.SubmitterProfileSnapshot,
            row.IsTestSubmission,
            row.CollectionStatus);
}
