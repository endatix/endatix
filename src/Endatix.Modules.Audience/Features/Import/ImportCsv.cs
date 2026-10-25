using Endatix.Core.Abstractions;
using Endatix.Core.Abstractions.Data;
using Endatix.Core.Entities;
using Endatix.Core.Infrastructure.Domain;
using Endatix.Core.Infrastructure.Messaging;
using Endatix.Core.Infrastructure.Result;
using Endatix.Modules.Audience.Domain;
using Endatix.Modules.Audience.Features.Imports;
using Endatix.Modules.Audience.Persistence;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Endatix.Modules.Audience.Features.Import;

/// <summary>
/// Sync-imports audience people from CSV (max 5,000 rows). Partial success is allowed. <see cref="IsTest"/> makes
/// the people this import adds to the form test persons; people already on the form get no writes and count as
/// skipped.
/// </summary>
public sealed record ImportCsvCommand(
    long TenantId,
    long FormId,
    string CsvText,
    string IdentifierColumn,
    string FileName,
    IReadOnlyDictionary<string, string>? PropertyColumns = null,
    long? PerformedById = null,
    bool IsTest = false) : ICommand<Result<ImportResultDto>>;

/// <summary>
/// Import outcome counts plus the first rejected rows with their reasons.
/// </summary>
public sealed record ImportResultDto(
    long ImportId,
    int CreatedCount,
    int UpdatedCount,
    int SkippedCount,
    int RejectedCount,
    IReadOnlyList<ImportRejectionDto> Rejections);

/// <summary>
/// One rejected CSV data row.
/// </summary>
public sealed record ImportRejectionDto(int RowNumber, string Reason);

/// <summary>
/// The one-request import, run as a staged import in the request: the file is parsed here, started and staged as
/// the import's chunks, then checked, confirmed and written by the same run the staged import uses. A form with an
/// import still running refuses it with 409, as a second start does.
/// </summary>
internal sealed class ImportCsvHandler(
    IAudienceDbContext db,
    IRepository<Form> forms,
    IMediator mediator,
    ImportRun run)
    : ICommandHandler<ImportCsvCommand, Result<ImportResultDto>>
{
    private const int MaxReportedRejections = 50;
    private const string Changed = "The audience changed while the import ran. Run the import again.";

    public async Task<Result<ImportResultDto>> Handle(
        ImportCsvCommand request,
        CancellationToken cancellationToken)
    {
        Result<CsvFileParseResult> file = await ReadFileAsync(request, cancellationToken);
        if (!file.IsSuccess)
        {
            return file.ToErrorResult<ImportResultDto>();
        }

        return file.Value.Rows.Count == 0
            ? await RecordEmptyAsync(request, file.Value, cancellationToken)
            : await ImportAsync(request, file.Value, cancellationToken);
    }

    private async Task<Result<ImportResultDto>> ImportAsync(
        ImportCsvCommand request,
        CsvFileParseResult file,
        CancellationToken cancellationToken)
    {
        Result<ImportViewDto> started = await mediator.Send(StartOf(request, file), cancellationToken);
        if (!started.IsSuccess)
        {
            return started.ToErrorResult<ImportResultDto>();
        }

        ImportRef import = new(request.TenantId, request.FormId, started.Value.ImportId);
        await StageAsync(import, file, cancellationToken);
        Result ran = await RunAsync(import, cancellationToken);
        return ran.IsSuccess ? await ResultOfAsync(import, cancellationToken) : ran.ToErrorResult<ImportResultDto>();
    }

    /// <summary>Checks the form and the request, then parses the file. Writes nothing.</summary>
    private async Task<Result<CsvFileParseResult>> ReadFileAsync(
        ImportCsvCommand request,
        CancellationToken cancellationToken)
    {
        Result formGate = await TenantFormGate.EnsureAsync(
            new FormGateRequest(forms, request.TenantId, request.FormId, cancellationToken));
        if (!formGate.IsSuccess)
        {
            return formGate.ToErrorResult<CsvFileParseResult>();
        }

        string? error = RequestError(request);
        CsvFileParseResult parsed = error is null ? CsvFileParser.Parse(request.CsvText) : CsvFileParseResult.Invalid(error);
        return parsed.IsSuccess
            ? Result.Success(parsed)
            : Result<CsvFileParseResult>.Invalid(new ValidationError(parsed.Error!));
    }

    private static string? RequestError(ImportCsvCommand request)
    {
        if (string.IsNullOrWhiteSpace(request.CsvText))
        {
            return "CSV content is required.";
        }

        return string.IsNullOrWhiteSpace(request.IdentifierColumn) ? "Identifier column is required." : null;
    }

    private static StartImportCommand StartOf(ImportCsvCommand request, CsvFileParseResult file) =>
        new(
            request.TenantId,
            request.FormId,
            request.FileName,
            file.Headers,
            file.Rows.Count,
            request.IdentifierColumn,
            request.PropertyColumns ?? new Dictionary<string, string>(),
            request.IsTest,
            request.PerformedById);

    /// <summary>
    /// Stores the file as the import's chunks, each row with its own spreadsheet number, and moves it to Checking in
    /// the same save.
    /// </summary>
    private async Task StageAsync(ImportRef target, CsvFileParseResult file, CancellationToken cancellationToken)
    {
        AudienceImport import = (await ImportQueries.FindAsync(db, target, cancellationToken))!;
        foreach ((CsvDataRow[] rows, int index) in file.Rows.Chunk(import.ChunkSize).Select((rows, index) => (rows, index)))
        {
            List<StagedRow> staged = [.. rows.Select(row => StagedRowOf(row, file.Headers))];
            db.ImportChunks.Add(AudienceImportChunk.Create(import, new ChunkPlace(index, rows[0].RowNumber), staged));
        }

        import.BeginCheck();
        await db.SaveChangesAsync(cancellationToken);
        ((DbContext)db).ChangeTracker.Clear();
    }

    private static StagedRow StagedRowOf(CsvDataRow row, IReadOnlyList<string> headers) =>
        new([.. headers.Select(header => row.Cells.TryGetValue(header, out string? cell) ? cell : string.Empty)], row.Malformed, row.RowNumber);

    private async Task<Result> RunAsync(ImportRef import, CancellationToken cancellationToken)
    {
        Result check = await run.RunAsync(import, ImportPhase.Check, cancellationToken);
        if (!check.IsSuccess)
        {
            return check;
        }

        return await ConfirmAsync(import, cancellationToken) ?? await WriteAsync(import, cancellationToken);
    }

    /// <summary>Confirms the checked import; a refusal when something moved it meanwhile.</summary>
    private async Task<Result?> ConfirmAsync(ImportRef target, CancellationToken cancellationToken)
    {
        AudienceImport? import = await ImportQueries.FindAsync(db, target, cancellationToken);
        if (import is not { Status: AudienceImportStatusCodes.Checked })
        {
            return Result.Conflict(Changed);
        }

        import.Confirm();
        try
        {
            await db.SaveChangesAsync(cancellationToken);
            return null;
        }
        catch (DbUpdateConcurrencyException)
        {
            return Result.Conflict(Changed);
        }
    }

    /// <summary>
    /// Writes the import. A write that cannot finish in this request stops resumable, so the form is free for the
    /// next import at once rather than after the sweep.
    /// </summary>
    private async Task<Result> WriteAsync(ImportRef import, CancellationToken cancellationToken)
    {
        Result written;
        try
        {
            written = await run.RunAsync(import, ImportPhase.Write, cancellationToken);
        }
        catch (DbUpdateException race) when (run.IsAudienceRace(race))
        {
            await StopAsync(import);
            return Result.Conflict(Changed);
        }
        catch
        {
            await StopAsync(import);
            throw;
        }

        return written.Status == ResultStatus.Unavailable ? await BusyAsync(import) : written;
    }

    private async Task<Result> BusyAsync(ImportRef import)
    {
        await StopAsync(import);
        return Result.Unavailable(MembershipLock.FormBusy);
    }

    private async Task StopAsync(ImportRef target)
    {
        ((DbContext)db).ChangeTracker.Clear();
        AudienceImport? import = await ImportQueries.FindAsync(db, target, CancellationToken.None);
        if (import is not { Status: AudienceImportStatusCodes.Writing })
        {
            return;
        }

        import.Fail(ImportSweep.WriteStalled, resumable: true);
        try
        {
            await db.SaveChangesAsync(CancellationToken.None);
        }
        catch (DbUpdateConcurrencyException)
        {
            // The run ended the import first.
        }
    }

    private async Task<Result<ImportResultDto>> ResultOfAsync(ImportRef target, CancellationToken cancellationToken)
    {
        ((DbContext)db).ChangeTracker.Clear();
        AudienceImport import = (await ImportQueries.FindAsync(db, target, cancellationToken))!;
        List<ImportRejectionDto> rejections = await db.ImportRejections.AsNoTracking()
            .Where(rejection => rejection.TenantId == import.TenantId && rejection.ImportId == import.Id)
            .OrderBy(rejection => rejection.RowNumber)
            .Take(MaxReportedRejections)
            .Select(rejection => new ImportRejectionDto(rejection.RowNumber, rejection.Reason))
            .ToListAsync(cancellationToken);
        return ToDto(import, rejections);
    }

    /// <summary>A file with no data rows still checks its columns, and is recorded as an import that wrote nothing.</summary>
    private async Task<Result<ImportResultDto>> RecordEmptyAsync(
        ImportCsvCommand request,
        CsvFileParseResult file,
        CancellationToken cancellationToken)
    {
        if (await ColumnsErrorAsync(request, file, cancellationToken) is { } error)
        {
            return Result<ImportResultDto>.Invalid(new ValidationError(error));
        }

        AudienceImport summary = new(new AudienceImportCreateArgs(
            request.TenantId, request.FormId, request.FileName, 0, 0, 0, 0, request.PerformedById));
        db.AudienceImports.Add(summary);
        await db.SaveChangesAsync(cancellationToken);
        return ToDto(summary, []);
    }

    private async Task<string?> ColumnsErrorAsync(
        ImportCsvCommand request,
        CsvFileParseResult file,
        CancellationToken cancellationToken)
    {
        if (!file.Headers.Contains(request.IdentifierColumn, StringComparer.OrdinalIgnoreCase))
        {
            return $"The CSV has no '{request.IdentifierColumn}' column.";
        }

        List<Property> properties = await db.Properties
            .Where(property => property.TenantId == request.TenantId && property.FormId == request.FormId)
            .ToListAsync(cancellationToken);
        return CsvColumnMapper.MappingError(
            new CsvColumnRequest(properties, file.Headers, request.IdentifierColumn, request.PropertyColumns));
    }

    private static ImportResultDto ToDto(AudienceImport import, IReadOnlyList<ImportRejectionDto> rejections) =>
        new(
            import.Id,
            import.CreatedCount,
            import.UpdatedCount,
            import.SkippedCount,
            import.RejectedCount,
            rejections);
}

/// <summary>
/// Identity normalizer, unique-index detection and the form's people lock for one import.
/// </summary>
internal sealed class ImportWriteSupport(
    IValueNormalizer normalizer,
    IUniqueConstraintViolationChecker violations,
    MembershipLock formPeopleLock)
{
    public IValueNormalizer Normalizer { get; } = normalizer;

    public IUniqueConstraintViolationChecker Violations { get; } = violations;

    /// <summary>
    /// Takes every stripe of the form's people lock (scope <c>AudienceFormPeople</c>) shared, after the tenant's
    /// match-key lock; <see cref="MembershipLock"/> owns it with the participant lock, so their order lives in one
    /// place.
    /// </summary>
    public MembershipLock FormPeopleLock { get; } = formPeopleLock;
}
