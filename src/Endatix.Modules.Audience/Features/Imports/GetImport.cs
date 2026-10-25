using Endatix.Core.Abstractions;
using Endatix.Core.Infrastructure.Messaging;
using Endatix.Core.Infrastructure.Result;
using Endatix.Modules.Audience.Domain;
using Endatix.Modules.Audience.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Endatix.Modules.Audience.Features.Imports;

/// <summary>
/// Reads one staged import, or with no id the form's import that is still unfinished. A running phase whose job was
/// never recorded, and that has not moved for <see cref="GetImportHandler.HealAfter"/>, is enqueued again on the way,
/// which the job's dedup key makes harmless.
/// </summary>
public sealed record GetImportQuery(long TenantId, long FormId, long? ImportId) : IQuery<Result<ImportViewDto>>;

internal sealed class GetImportHandler(IAudienceDbContext db, IImportRunner runner, IDateTimeProvider clock)
    : IQueryHandler<GetImportQuery, Result<ImportViewDto>>
{
    /// <summary>
    /// A one-request import runs its phases in the request with no job and saves its progress every chunk and batch, so
    /// only a phase quiet this long is taken for one whose enqueue was lost.
    /// </summary>
    internal static readonly TimeSpan HealAfter = TimeSpan.FromMinutes(1);

    public async Task<Result<ImportViewDto>> Handle(GetImportQuery request, CancellationToken cancellationToken)
    {
        if (request.TenantId <= 0)
        {
            return Result<ImportViewDto>.Unauthorized("Tenant context is required.");
        }

        AudienceImport? import = request.ImportId is { } id
            ? await ImportQueries.FindAsync(db, new ImportRef(request.TenantId, request.FormId, id), cancellationToken)
            : await StartImportHandler.Unfinished(db, request.TenantId, request.FormId).FirstOrDefaultAsync(cancellationToken);
        if (import is null)
        {
            return Result<ImportViewDto>.NotFound("Import not found.");
        }

        await HealAsync(import, cancellationToken);
        return Result.Success(await ViewAsync(import, cancellationToken));
    }

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
        if (phase is { } running && runner.RunsInBackground && import.JobId is null && IsQuiet(import))
        {
            await runner.StartAsync(new ImportRef(import.TenantId, import.FormId, import.Id), running, cancellationToken);
            await ((DbContext)db).Entry(import).ReloadAsync(cancellationToken);
        }
    }

    private bool IsQuiet(AudienceImport import) =>
        (import.ModifiedAt ?? import.CreatedAt) < clock.Now.UtcDateTime - HealAfter;
}
