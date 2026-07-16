namespace Endatix.Api.Endpoints.Submissions;

public class ExportRequest
{
    public long FormId { get; set; }

    public string? ExportFormat { get; set; }

    public long? ExportId { get; set; }

    public long? ExportFormatId { get; set; }

    public bool? IncludeTestSubmissions { get; set; }

    public string[]? ColumnScope { get; set; }

    /// <summary>
    /// Optional codebook label locale for this export run. Overrides format settings.
    /// </summary>
    public string? Locale { get; set; }
}