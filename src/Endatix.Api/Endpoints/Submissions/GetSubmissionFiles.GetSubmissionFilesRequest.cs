namespace Endatix.Api.Endpoints.Submissions;

public class GetSubmissionFilesRequest : IManagedSubmissionTarget
{
    public long SubmissionId { get; set; }
    public long FormId { get; set; }

    public string? FileNamesPrefix { get; set; }
}