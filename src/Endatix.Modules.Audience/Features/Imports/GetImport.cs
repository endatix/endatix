using Endatix.Core.Infrastructure.Messaging;
using Endatix.Core.Infrastructure.Result;
using Endatix.Modules.Audience.Domain;
using Endatix.Modules.Audience.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Endatix.Modules.Audience.Features.Imports;

/// <summary>
/// Reads one staged import, or with no id the form's current import: the one still unfinished, else its newest
/// stopped write that can still resume, so the scripter is offered Resume after the fact. A running phase whose job was
/// never recorded is enqueued again on the way, which the job's dedup key makes harmless, unless the phase runs in a
/// request (<see cref="AudienceImport.RunsInRequest"/>).
/// </summary>
public sealed record GetImportQuery(long TenantId, long FormId, long? ImportId) : IQuery<Result<ImportViewDto>>;

internal sealed class GetImportHandler(IAudienceDbContext db, IImportRunner runner)
    : IQueryHandler<GetImportQuery, Result<ImportViewDto>>
{
    public async Task<Result<ImportViewDto>> Handle(GetImportQuery request, CancellationToken cancellationToken)
    {
        if (request.TenantId <= 0)
        {
            return Result<ImportViewDto>.Unauthorized("Tenant context is required.");
        }

        AudienceImport? import = request.ImportId is { } id
            ? await ImportQueries.FindAsync(db, new ImportRef(request.TenantId, request.FormId, id), cancellationToken)
            : await CurrentAsync(request, cancellationToken);
        if (import is null)
        {
            return Result<ImportViewDto>.NotFound("Import not found.");
        }

        await HealAsync(import, cancellationToken);
        return Result.Success(await ViewAsync(import, cancellationToken));
    }

    /// <summary>The unfinished import, else the newest stopped write that can resume, read in one statement.</summary>
    private Task<AudienceImport?> CurrentAsync(GetImportQuery request, CancellationToken cancellationToken) =>
        db.AudienceImports
            .Where(import => import.TenantId == request.TenantId
                && import.FormId == request.FormId
                && (AudienceImportStatusCodes.Unfinished.Contains(import.Status)
                    || (import.Status == AudienceImportStatusCodes.Failed && import.Resumable)))
            .OrderBy(import => import.Status == AudienceImportStatusCodes.Failed ? 1 : 0)
            .ThenByDescending(import => import.Id)
            .FirstOrDefaultAsync(cancellationToken);

    private Task<ImportViewDto> ViewAsync(AudienceImport import, CancellationToken cancellationToken) =>
        ImportView.ReadAsync(new ImportViewSource(db, runner.RunsInBackground), import, cancellationToken);

    // A phase is committed before its job is enqueued, so a crash between the two leaves a running import with no job.
    private async Task HealAsync(AudienceImport import, CancellationToken cancellationToken)
    {
        ImportPhase? phase = import.Status switch
        {
            AudienceImportStatusCodes.Checking => ImportPhase.Check,
            AudienceImportStatusCodes.Writing => ImportPhase.Write,
            _ => null,
        };
        if (phase is { } running && runner.RunsInBackground && import.JobId is null && !import.RunsInRequest)
        {
            await runner.StartAsync(new ImportRef(import.TenantId, import.FormId, import.Id), running, cancellationToken);
            await ((DbContext)db).Entry(import).ReloadAsync(cancellationToken);
        }
    }
}
