using MediatR;
using Endatix.Core.Abstractions.Repositories;
using Endatix.Core.Entities;

namespace Endatix.Core.UseCases.Submissions.Export;

public sealed class GetFormSubmissionsExportQueryHandler : IRequestHandler<GetFormSubmissionsExportQuery, GetFormSubmissionsExportQueryResponse>
{
    private readonly ISubmissionExportRepository _exportRepository;

    public GetFormSubmissionsExportQueryHandler(ISubmissionExportRepository exportRepository)
    {
        _exportRepository = exportRepository;
    }

    public async Task<GetFormSubmissionsExportQueryResponse> Handle(GetFormSubmissionsExportQuery request, CancellationToken cancellationToken)
    {
        var exportRows = _exportRepository.GetExportRowsAsync(request.FormId, cancellationToken);
        // For now, just return a placeholder response
        return new GetFormSubmissionsExportQueryResponse(
            FileContent: Array.Empty<byte>(),
            ContentType: "text/csv",
            FileName: $"submissions-{request.FormId}.csv"
        );
    }
} 