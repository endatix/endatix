using Endatix.Core.Abstractions;
using Endatix.Core.Abstractions.Data;
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
    IRepository<Form> forms,
    IValueNormalizer normalizer,
    IUniqueConstraintViolationChecker violations)
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

        Result<ImportInput> prepared = await PrepareAsync(request, cancellationToken);
        return prepared.IsSuccess
            ? await ImportWithRetryAsync(request, prepared.Value, cancellationToken)
            : prepared.ToErrorResult<ImportResultDto>();
    }

    /// <summary>
    /// A parallel add or import can win a unique index first. Its rows are committed by then, so
    /// a second pass reloads them and matches instead of inserting. A second loss is a conflict.
    /// </summary>
    private async Task<Result<ImportResultDto>> ImportWithRetryAsync(
        ImportCsvCommand request,
        ImportInput input,
        CancellationToken cancellationToken)
    {
        try
        {
            return await ImportAsync(request, input, cancellationToken);
        }
        catch (DbUpdateException ex) when (IsAudienceRace(ex))
        {
            ((DbContext)db).ChangeTracker.Clear();
            return await ImportOnceMoreAsync(request, input, cancellationToken);
        }
    }

    private async Task<Result<ImportResultDto>> ImportOnceMoreAsync(
        ImportCsvCommand request,
        ImportInput input,
        CancellationToken cancellationToken)
    {
        try
        {
            return await ImportAsync(request, input, cancellationToken);
        }
        catch (DbUpdateException ex) when (IsAudienceRace(ex))
        {
            return Result.Conflict("The audience changed while the import ran. Run the import again.");
        }
    }

    private bool IsAudienceRace(DbUpdateException ex) =>
        violations.IsViolationOf(ex, Member.UniqueConstraints.IdentifierPerTenant)
        || violations.IsViolationOf(ex, Membership.UniqueConstraints.MemberPerForm)
        || violations.IsViolationOf(ex, PropertyValue.UniqueConstraints.CellPerMembership);

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

    /// <summary>
    /// Parses the file and resolves the column mapping. Writes nothing, so a refused file or
    /// mapping leaves the audience as it was.
    /// </summary>
    private async Task<Result<ImportInput>> PrepareAsync(
        ImportCsvCommand request,
        CancellationToken cancellationToken)
    {
        CsvFileParseResult parsed = CsvFileParser.Parse(request.CsvText);
        string? fileError = FileError(parsed, request.IdentifierColumn);
        if (fileError is not null)
        {
            return Result<ImportInput>.Invalid(new ValidationError(fileError));
        }

        List<Property> properties = await LoadPropertiesAsync(request.FormId, cancellationToken);
        return MapColumns(request, parsed, properties);
    }

    private static Result<ImportInput> MapColumns(
        ImportCsvCommand request,
        CsvFileParseResult parsed,
        IReadOnlyList<Property> properties)
    {
        string? mappingError = CsvColumnMapper.MappingError(properties, parsed.Headers, request.PropertyColumns);
        return mappingError is null
            ? Result.Success(new ImportInput(
                parsed.Rows,
                CsvColumnMapper.Build(properties, request.IdentifierColumn, request.PropertyColumns)))
            : Result<ImportInput>.Invalid(new ValidationError(mappingError));
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
    private async Task<Result<ImportResultDto>> ImportAsync(
        ImportCsvCommand request,
        ImportInput input,
        CancellationToken cancellationToken)
    {
        await using IDbContextTransaction transaction =
            await MatchKeyLock.BeginSharedAsync(db, request.TenantId, cancellationToken);
        string identifierKind = await IdentifierKindReader.GetAsync(db, request.TenantId, cancellationToken);
        ImportTarget target = new(request.TenantId, request.FormId, new ImportMatchKey(identifierKind, normalizer));
        ImportRows rows = ImportRowReader.Read(input.Rows, input.Map, target.MatchKey);
        ImportTally tally = await StageAsync(new AudienceQuery(db, target, cancellationToken), rows.Accepted);

        AudienceImport summary = new(ToCreateArgs(request, tally, rows.Rejected.Count));
        db.AudienceImports.Add(summary);
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return ToDto(summary, rows.Rejected);
    }

    private Task<List<Property>> LoadPropertiesAsync(long formId, CancellationToken cancellationToken) =>
        db.Properties
            .Where(property => property.FormId == formId)
            .ToListAsync(cancellationToken);

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

/// <summary>
/// A parsed file and the column mapping resolved against this form's properties.
/// </summary>
internal sealed record ImportInput(IReadOnlyList<CsvDataRow> Rows, CsvColumnMap Map);
