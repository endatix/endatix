namespace Endatix.Core.UseCases.Submissions.Export;

/// <summary>
/// DEPRECATED: This class is replaced by FormSubmissionsExportResult which supports streaming.
/// This will be removed in a future update.
/// </summary>
public sealed record SubmissionsExportResponse(
    byte[] FileContent,
    string ContentType,
    string FileName
); 