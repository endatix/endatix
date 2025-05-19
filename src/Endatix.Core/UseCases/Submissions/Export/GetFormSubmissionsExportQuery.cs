using MediatR;

namespace Endatix.Core.UseCases.Submissions.Export;

public sealed record GetFormSubmissionsExportQuery(
    long FormId,
    string ExportFormat // e.g., "csv", "json", "excel", "googledrive"
) : IRequest<GetFormSubmissionsExportQueryResponse>; 