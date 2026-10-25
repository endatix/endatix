using Endatix.Core.Abstractions.Data;
using Endatix.Core.Infrastructure.Messaging;
using Endatix.Core.Infrastructure.Result;
using Endatix.Modules.Audience.Domain;
using Endatix.Modules.Audience.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Endatix.Modules.Audience.Features.Imports;

/// <summary>
/// Stores one chunk of an uploading import. Sending a chunk again replaces it, so a retried upload stores each
/// chunk once. The chunk that completes the file starts the Check.
/// </summary>
public sealed record StageImportChunkCommand(
    ImportRef Import,
    int Index,
    int FirstRowNumber,
    IReadOnlyList<StagedRow> Rows) : ICommand<Result<ImportViewDto>>;

internal sealed class StageImportChunkHandler(
    IAudienceDbContext db,
    IImportRunner runner,
    IUniqueConstraintViolationChecker violations)
    : ICommandHandler<StageImportChunkCommand, Result<ImportViewDto>>
{
    public async Task<Result<ImportViewDto>> Handle(StageImportChunkCommand request, CancellationToken cancellationToken)
    {
        AudienceImport? import = await ImportQueries.FindAsync(db, request.Import, cancellationToken);
        if (Refusal(import, request) is { } refused)
        {
            return refused;
        }

        await StoreAsync(import!, request, cancellationToken);
        Result started = await StartCheckIfCompleteAsync(import!, request.Import, cancellationToken);
        return started.Status == ResultStatus.Unavailable
            ? started.ToErrorResult<ImportViewDto>()
            : Result.Success(await ViewAsync(import!, cancellationToken));
    }

    private static Result<ImportViewDto>? Refusal(AudienceImport? import, StageImportChunkCommand request)
    {
        if (import is null)
        {
            return Result<ImportViewDto>.NotFound("Import not found.");
        }

        if (import.Status != AudienceImportStatusCodes.Uploading)
        {
            return Result<ImportViewDto>.Conflict($"The import is {import.Status}; it takes no more rows.");
        }

        string? error = IndexError(import, request) ?? RowsError(import, request);
        return error is null ? null : Result<ImportViewDto>.Invalid(new ValidationError(error));
    }

    private static string? IndexError(AudienceImport import, StageImportChunkCommand request)
    {
        if (request.Index < 0 || request.Index >= import.ChunkCount)
        {
            return $"The import has chunks 0 to {import.ChunkCount - 1}.";
        }

        int expected = Math.Min(import.ChunkSize, import.RowCount - (request.Index * import.ChunkSize));
        return request.Rows.Count == expected ? null : $"Chunk {request.Index} holds {expected} rows, not {request.Rows.Count}.";
    }

    private static string? RowsError(AudienceImport import, StageImportChunkCommand request)
    {
        if (request.FirstRowNumber < 2)
        {
            return "Data rows start after the header row, at row 2.";
        }

        int columns = import.Headers.Count;
        return request.Rows.Any(row => row.Cells.Count > columns)
            ? $"A row has more cells than the file's {columns} columns."
            : null;
    }

    /// <summary>Starts the Check when this chunk completed the file and no parallel last chunk started it first.</summary>
    private async Task<Result> StartCheckIfCompleteAsync(
        AudienceImport import,
        ImportRef target,
        CancellationToken cancellationToken)
    {
        if (!await IsCompleteAsync(import, cancellationToken) || !await BeginCheckAsync(import, cancellationToken))
        {
            return Result.Success();
        }

        Result started = await runner.StartAsync(target, ImportPhase.Check, cancellationToken);
        await ((DbContext)db).Entry(import).ReloadAsync(cancellationToken);
        return started;
    }

    private Task<ImportViewDto> ViewAsync(AudienceImport import, CancellationToken cancellationToken) =>
        ImportView.ReadAsync(new ImportViewSource(db, runner.RunsInBackground), import, cancellationToken);

    private async Task StoreAsync(AudienceImport import, StageImportChunkCommand request, CancellationToken cancellationToken)
    {
        await db.ImportChunks
            .Where(chunk => chunk.TenantId == import.TenantId
                && chunk.ImportId == import.Id
                && chunk.ChunkIndex == request.Index)
            .ExecuteDeleteAsync(cancellationToken);
        AudienceImportChunk chunk = AudienceImportChunk.Create(import, new ChunkPlace(request.Index, request.FirstRowNumber), request.Rows);
        db.ImportChunks.Add(chunk);
        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException race)
            when (violations.IsViolationOf(race, AudienceImportChunk.UniqueConstraints.OnePerIndex))
        {
            // The same chunk, sent twice at once: the other copy is stored.
            ((DbContext)db).Entry(chunk).State = EntityState.Detached;
        }
    }

    private async Task<bool> IsCompleteAsync(AudienceImport import, CancellationToken cancellationToken) =>
        await ImportView.StagedIndexes(db, import).CountAsync(cancellationToken) == import.ChunkCount;

    /// <summary>Moves the import to Checking; false when a parallel last chunk did it first.</summary>
    private async Task<bool> BeginCheckAsync(AudienceImport import, CancellationToken cancellationToken)
    {
        import.BeginCheck();
        try
        {
            await db.SaveChangesAsync(cancellationToken);
            return true;
        }
        catch (DbUpdateConcurrencyException)
        {
            await ((DbContext)db).Entry(import).ReloadAsync(cancellationToken);
            return false;
        }
    }
}

internal static class ImportQueries
{
    public static Task<AudienceImport?> FindAsync(IAudienceDbContext db, ImportRef import, CancellationToken cancellationToken) =>
        db.AudienceImports.SingleOrDefaultAsync(
            item => item.Id == import.ImportId && item.TenantId == import.TenantId && item.FormId == import.FormId,
            cancellationToken);
}
