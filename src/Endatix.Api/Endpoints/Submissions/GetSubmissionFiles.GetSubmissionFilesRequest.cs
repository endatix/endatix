namespace Endatix.Api.Endpoints.Submissions;

public class GetSubmissionFilesRequest
{
    public long SubmissionId { get; set; }
    public long FormId { get; set; }

    public string? FileNamesPrefix { get; set; }
}