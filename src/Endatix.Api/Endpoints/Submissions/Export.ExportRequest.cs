namespace Endatix.Api.Endpoints.Submissions;

public class ExportRequest
{
    public long FormId { get; set; }

    public string? ExportFormat { get; set; }

    public long? ExportId { get; set; }
}