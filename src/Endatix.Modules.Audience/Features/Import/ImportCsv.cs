using Endatix.Core.Entities;
using Endatix.Core.Infrastructure.Domain;
using Endatix.Core.Infrastructure.Messaging;
using Endatix.Core.Infrastructure.Result;
using Endatix.Modules.Audience.Domain;
using Endatix.Modules.Audience.Features.Settings;
using Endatix.Modules.Audience.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace Endatix.Modules.Audience.Features.Import;

/// <summary>
/// Sync-imports audience people from CSV (max 5,000 rows). Partial success is allowed.
/// </summary>
public sealed record ImportCsvCommand(
    long TenantId,
    long FormId,
    string CsvText,
    string IdentifierColumn,
    string FileName,
    IReadOnlyDictionary<string, string>? PropertyColumns = null,
    long? PerformedById = null) : ICommand<Result<ImportResultDto>>;

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

internal sealed class ImportCsvHandler(
    IAudienceDbContext db,
    IRepository<Form> forms)
    : ICommandHandler<ImportCsvCommand, Result<ImportResultDto>>
{
    private const int MaxReportedRejections = 50;

    public async Task<Result<ImportResultDto>> Handle(
        ImportCsvCommand request,
        CancellationToken cancellationToken)
    {
        Result gate = await ValidateRequestAsync(request, cancellationToken);
        if (!gate.IsSuccess)
        {
            return gate.ToErrorResult<ImportResultDto>();
        }

        CsvFileParseResult parsed = CsvFileParser.Parse(request.CsvText);
        string? fileError = FileError(parsed, request.IdentifierColumn);
        return fileError is null
            ? Result.Success(await ImportAsync(request, parsed.Rows, cancellationToken))
            : Result.Invalid(new ValidationError(fileError));
    }

    private async Task<Result> ValidateRequestAsync(
        ImportCsvCommand request,
        CancellationToken cancellationToken)
    {
        Result formGate = await TenantFormGate.EnsureAsync(
            new FormGateRequest(forms, request.TenantId, request.FormId, cancellationToken));
        if (!formGate.IsSuccess)
        {
            return formGate;
        }

        if (string.IsNullOrWhiteSpace(request.CsvText))
        {
            return Result.Invalid(new ValidationError("CSV content is required."));
        }

        return string.IsNullOrWhiteSpace(request.IdentifierColumn)
            ? Result.Invalid(new ValidationError("Identifier column is required."))
            : Result.Success();
    }

    private static string? FileError(CsvFileParseResult parsed, string identifierColumn)
    {
        if (!parsed.IsSuccess)
        {
            return parsed.Error;
        }

        return parsed.Headers.Contains(identifierColumn, StringComparer.OrdinalIgnoreCase)
            ? null
            : $"The CSV has no '{identifierColumn}' column.";
    }

    /// <summary>
    /// Stages every row and the import summary, then saves once: a database failure leaves no
    /// half-imported audience and no summary that disagrees with the rows written.
    /// </summary>
    private async Task<ImportResultDto> ImportAsync(
        ImportCsvCommand request,
        IReadOnlyList<IReadOnlyDictionary<string, string>> csvRows,
        CancellationToken cancellationToken)
    {
        await using IDbContextTransaction transaction =
            await MatchKeyLock.BeginAsync(db, request.TenantId, cancellationToken);
        ImportTarget target = new(
            request.TenantId,
            request.FormId,
            await IdentifierKindReader.GetAsync(db, request.TenantId, cancellationToken));
        ImportRows rows = ImportRowReader.Read(
            csvRows, await BuildMapAsync(request, cancellationToken), target.IdentifierKind);
        ImportTally tally = await StageAsync(new AudienceQuery(db, target, cancellationToken), rows.Accepted);

        AudienceImport summary = new(ToCreateArgs(request, tally, rows.Rejected.Count));
        db.AudienceImports.Add(summary);
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return ToDto(summary, rows.Rejected);
    }

    private async Task<CsvColumnMap> BuildMapAsync(
        ImportCsvCommand request,
        CancellationToken cancellationToken)
    {
        List<Property> properties = await db.Properties
            .Where(property => property.FormId == request.FormId)
            .ToListAsync(cancellationToken);
        return CsvColumnMapper.Build(properties, request.IdentifierColumn, request.PropertyColumns);
    }

    private static async Task<ImportTally> StageAsync(AudienceQuery query, IReadOnlyList<ImportRow> rows)
    {
        ExistingAudience existing = await ExistingAudience.LoadAsync(query, rows);
        ImportBatch batch = new(query.Db, query.Target, existing);
        foreach (ImportRow row in rows)
        {
            batch.Stage(row);
        }

        return batch.Tally;
    }

    private static AudienceImportCreateArgs ToCreateArgs(
        ImportCsvCommand request,
        ImportTally tally,
        int rejectedCount) =>
        new(
            request.TenantId,
            request.FormId,
            request.FileName,
            tally.Created,
            tally.Updated,
            tally.Skipped,
            rejectedCount,
            request.PerformedById);

    private static ImportResultDto ToDto(AudienceImport summary, IReadOnlyList<ImportRejectionDto> rejected) =>
        new(
            summary.Id,
            summary.CreatedCount,
            summary.UpdatedCount,
            summary.SkippedCount,
            summary.RejectedCount,
            rejected.Take(MaxReportedRejections).ToList());
}
