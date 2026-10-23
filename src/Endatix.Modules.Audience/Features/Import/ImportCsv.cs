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

internal sealed class ImportCsvHandler(
    IAudienceDbContext db,
    IRepository<Form> forms,
    ImportWriteSupport support,
    MatchKeyLock matchKeyLock)
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
    /// A parallel add or import can win a unique index first, or a removal can take a person this import updates. Its
    /// rows are committed by then, so a second pass reloads them: it matches instead of inserting, and adds a removed
    /// person again. A second loss is a conflict.
    /// </summary>
    private async Task<Result<ImportResultDto>> ImportWithRetryAsync(
        ImportCsvCommand request,
        ImportInput input,
        CancellationToken cancellationToken)
    {
        Result<ImportResultDto>? first = await ImportUnlessRaceAsync(request, input, cancellationToken);
        if (first is not null)
        {
            return first;
        }

        ((DbContext)db).ChangeTracker.Clear();
        return await ImportUnlessRaceAsync(request, input, cancellationToken)
            ?? Result.Conflict("The audience changed while the import ran. Run the import again.");
    }

    /// <summary>
    /// The import's result, or <see langword="null"/> when a parallel write won a unique index or removed a person
    /// this import updates.
    /// </summary>
    private async Task<Result<ImportResultDto>?> ImportUnlessRaceAsync(
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
            return null;
        }
    }

    private bool IsAudienceRace(DbUpdateException ex) =>
        support.Violations.IsViolationOf(ex, Member.UniqueConstraints.IdentifierPerTenant)
        || support.Violations.IsViolationOf(ex, Membership.UniqueConstraints.MemberPerForm)
        || support.Violations.IsViolationOf(ex, PropertyValue.UniqueConstraints.CellPerMembership);

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

        List<Property> properties = await LoadPropertiesAsync(request, cancellationToken);
        return MapColumns(request, parsed, properties);
    }

    private static Result<ImportInput> MapColumns(
        ImportCsvCommand request,
        CsvFileParseResult parsed,
        IReadOnlyList<Property> properties)
    {
        CsvColumnRequest columns = new(properties, parsed.Headers, request.IdentifierColumn, request.PropertyColumns);
        string? mappingError = CsvColumnMapper.MappingError(columns);
        return mappingError is null
            ? Result.Success(new ImportInput(parsed.Rows, CsvColumnMapper.Build(columns)))
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
    /// half-imported audience and no summary that disagrees with the rows written. <see langword="null"/>, with
    /// nothing saved, when a person it updates was removed after it read them.
    /// </summary>
    private async Task<Result<ImportResultDto>?> ImportAsync(
        ImportCsvCommand request,
        ImportInput input,
        CancellationToken cancellationToken)
    {
        await using IDbContextTransaction transaction =
            await matchKeyLock.BeginSharedAsync(db, request.TenantId, cancellationToken);
        ImportTarget target = await TargetAsync(request, cancellationToken);
        ImportRows rows = ImportRowReader.Read(input.Rows, input.Map, target.MatchKey);
        if (await StageAsync(new AudienceQuery(db, target, cancellationToken), rows.Accepted) is not { } tally)
        {
            return null;
        }

        AudienceImport summary = db.AudienceImports.Add(new(ToCreateArgs(request, tally, rows.Rejected.Count))).Entity;
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return ToDto(summary, rows.Rejected);
    }

    /// <summary>Read under the match-key lock, so a key change cannot land between the read and the save.</summary>
    private async Task<ImportTarget> TargetAsync(ImportCsvCommand request, CancellationToken cancellationToken)
    {
        string identifierKind = await IdentifierKindReader.GetAsync(db, request.TenantId, cancellationToken);
        return new ImportTarget(
            request.TenantId, request.FormId, new ImportMatchKey(identifierKind, support.Normalizer), request.IsTest);
    }

    private Task<List<Property>> LoadPropertiesAsync(ImportCsvCommand request, CancellationToken cancellationToken) =>
        db.Properties
            .Where(property => property.TenantId == request.TenantId && property.FormId == request.FormId)
            .ToListAsync(cancellationToken);

    /// <summary>
    /// Stages every row, then claims the memberships it updates. <see langword="null"/> when a person it updates was
    /// removed after it read them.
    /// </summary>
    private static async Task<ImportTally?> StageAsync(AudienceQuery query, IReadOnlyList<ImportRow> rows)
    {
        ExistingAudience existing = await ExistingAudience.LoadAsync(query, rows);
        ImportBatch batch = new(query.Db, query.Target, existing);
        foreach (ImportRow row in rows)
        {
            batch.Stage(row);
        }

        return await batch.ClaimUpdatedAsync(query.CancellationToken) ? batch.Tally : null;
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

/// <summary>
/// Identity normalizer and unique-index detection for one import.
/// </summary>
internal sealed class ImportWriteSupport(
    IValueNormalizer normalizer,
    IUniqueConstraintViolationChecker violations)
{
    public IValueNormalizer Normalizer { get; } = normalizer;

    public IUniqueConstraintViolationChecker Violations { get; } = violations;
}
