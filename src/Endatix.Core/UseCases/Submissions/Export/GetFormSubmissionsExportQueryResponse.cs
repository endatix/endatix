namespace Endatix.Core.UseCases.Submissions.Export;

public sealed record GetFormSubmissionsExportQueryResponse(
    byte[] FileContent,
    string ContentType,
    string FileName
); 