using Endatix.Core.Abstractions.Data;
using Endatix.Core.Exceptions;
using Endatix.Core.Infrastructure.Messaging;
using Endatix.Core.Infrastructure.Result;
using Endatix.Modules.Audience.Domain;
using Endatix.Modules.Audience.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Endatix.Modules.Audience.Features.Imports;

/// <summary>
/// The scripter's decisions on a staged import: <c>writing</c> confirms a checked import or resumes a stopped write,
/// <c>canceled</c> stops it before anything is written.
/// </summary>
public sealed record MoveImportCommand(ImportRef Import, string Status) : ICommand<Result<ImportViewDto>>;

internal sealed class MoveImportHandler(IAudienceDbContext db, IImportRunner runner, ImportMoveGuard guard)
    : ICommandHandler<MoveImportCommand, Result<ImportViewDto>>
{
    public async Task<Result<ImportViewDto>> Handle(MoveImportCommand request, CancellationToken cancellationToken)
    {
        AudienceImport? import = await ImportQueries.FindAsync(db, request.Import, cancellationToken);
        if (import is null)
        {
            return Result<ImportViewDto>.NotFound("Import not found.");
        }

        if (await MoveAsync(import, request.Status, cancellationToken) is { } refused)
        {
            return refused;
        }

        Result started = await StartWriteIfWritingAsync(import, request.Import, cancellationToken);
        return started.Status == ResultStatus.Unavailable
            ? started.ToErrorResult<ImportViewDto>()
            : Result.Success(await ViewAsync(import, cancellationToken));
    }

    /// <summary>Moves and saves the import, or says why it cannot move.</summary>
    private async Task<Result<ImportViewDto>?> MoveAsync(
        AudienceImport import,
        string status,
        CancellationToken cancellationToken)
    {
        try
        {
            await ApplyAsync(import, status, cancellationToken);
            return null;
        }
        catch (DomainRuleException ex)
        {
            return guard.Rule(ex, import.Id, status);
        }
        catch (DbUpdateException ex) when (guard.Save(ex) is { } conflict)
        {
            return conflict;
        }
    }

    private async Task<Result> StartWriteIfWritingAsync(
        AudienceImport import,
        ImportRef target,
        CancellationToken cancellationToken)
    {
        if (import.Status != AudienceImportStatusCodes.Writing)
        {
            return Result.Success();
        }

        Result started = await runner.StartAsync(target, ImportPhase.Write, cancellationToken);
        await ((DbContext)db).Entry(import).ReloadAsync(cancellationToken);
        return started;
    }

    private Task<ImportViewDto> ViewAsync(AudienceImport import, CancellationToken cancellationToken) =>
        ImportView.ReadAsync(new ImportViewSource(db, runner.RunsInBackground), import, cancellationToken);

    private async Task ApplyAsync(AudienceImport import, string status, CancellationToken cancellationToken)
    {
        Move(import, status);
        await db.SaveChangesAsync(cancellationToken);
    }

    private static void Move(AudienceImport import, string status)
    {
        switch (status)
        {
            case AudienceImportStatusCodes.Writing when import.Status == AudienceImportStatusCodes.Failed:
                import.Resume();
                break;
            case AudienceImportStatusCodes.Writing:
                import.Confirm();
                break;
            case AudienceImportStatusCodes.Canceled:
                import.Cancel();
                break;
            default:
                throw new DomainRuleException("An import can only be moved to writing or canceled.");
        }
    }
}

/// <summary>Why a move's save is a conflict: someone else moved the import, or the form already has one.</summary>
internal sealed class ImportMoveGuard(IUniqueConstraintViolationChecker violations, ILogger<ImportMoveGuard> logger)
{
    private const string Changed = "The import changed meanwhile. Reload it and try again.";

    private const string Occupied = "This form already has an import in progress.";

    public Result<ImportViewDto> Rule(DomainRuleException ex, long importId, string status) =>
        Result<ImportViewDto>.Conflict(SafeError.LogAndResolve(
            logger, ex, Changed, $"moving import {importId} to {status}"));

    public Result<ImportViewDto>? Save(DbUpdateException ex)
    {
        if (ex is DbUpdateConcurrencyException)
        {
            return Result<ImportViewDto>.Conflict(Changed);
        }

        return violations.IsViolationOf(ex, AudienceImport.UniqueConstraints.OneUnfinishedPerForm)
            ? Result<ImportViewDto>.Conflict(Occupied)
            : null;
    }
}
