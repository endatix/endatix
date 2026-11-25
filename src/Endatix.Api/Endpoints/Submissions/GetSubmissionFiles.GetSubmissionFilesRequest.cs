namespace Endatix.Api.Endpoints.Submissions;

/// <summary>Selects the submission whose files are downloaded.</summary>
public class GetSubmissionFilesRequest
{
    public long SubmissionId { get; set; }
    public long FormId { get; set; }

    public string? FileNamesPrefix { get; set; }
}
