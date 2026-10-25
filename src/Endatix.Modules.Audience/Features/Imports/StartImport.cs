using Endatix.Core.Abstractions.Data;
using Endatix.Core.Entities;
using Endatix.Core.Infrastructure.Domain;
using Endatix.Core.Infrastructure.Messaging;
using Endatix.Core.Infrastructure.Result;
using Endatix.Modules.Audience.Domain;
using Endatix.Modules.Audience.Features.Import;
using Endatix.Modules.Audience.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Endatix.Modules.Audience.Features.Imports;

/// <summary>
/// Starts a staged import: the file's headers, its declared row count and the column mapping, fixed from here on.
/// A form has at most one import it can still change; a second start answers 409 with that import's id.
/// </summary>
public sealed record StartImportCommand(
    long TenantId,
    long FormId,
    string FileName,
    IReadOnlyList<string> Headers,
    int RowCount,
    string IdentifierColumn,
    IReadOnlyDictionary<string, string> PropertyColumns,
    bool IsTest,
    long? PerformedById) : ICommand<Result<ImportViewDto>>;

internal sealed class StartImportHandler(
    IAudienceDbContext db,
    IRepository<Form> forms,
    IImportRunner runner,
    IUniqueConstraintViolationChecker violations,
    ImportSweep sweep)
    : ICommandHandler<StartImportCommand, Result<ImportViewDto>>
{
    internal const string ImportExistsCode = "import_exists";
    private const string ImportExists = "This form already has an import in progress.";

    public async Task<Result<ImportViewDto>> Handle(StartImportCommand request, CancellationToken cancellationToken)
    {
        Result<ImportViewDto>? refused = await RefusalAsync(request, cancellationToken);
        if (refused is not null)
        {
            return refused;
        }

        AudienceImport import = AudienceImport.Start(StartArgsOf(request));
        return await TryAddAsync(import, cancellationToken)
            ? Result<ImportViewDto>.Created(await ViewAsync(import, cancellationToken))
            : await ConflictWithCurrentAsync(request, cancellationToken);
    }

    /// <summary>Adds the import; false when a parallel start on the form won the unfinished-import index.</summary>
    private async Task<bool> TryAddAsync(AudienceImport import, CancellationToken cancellationToken)
    {
        db.AudienceImports.Add(import);
        try
        {
            await db.SaveChangesAsync(cancellationToken);
            return true;
        }
        catch (DbUpdateException race)
            when (violations.IsViolationOf(race, AudienceImport.UniqueConstraints.OneUnfinishedPerForm))
        {
            return false;
        }
    }

    /// <summary>Why the import cannot start: no form, a file this host does not take, or an import already running.</summary>
    private async Task<Result<ImportViewDto>?> RefusalAsync(StartImportCommand request, CancellationToken cancellationToken)
    {
        Result gate = await TenantFormGate.EnsureAsync(
            new FormGateRequest(forms, request.TenantId, request.FormId, cancellationToken));
        if (!gate.IsSuccess)
        {
            return gate.ToErrorResult<ImportViewDto>();
        }

        if (await RequestErrorAsync(request, cancellationToken) is { } error)
        {
            return Result<ImportViewDto>.Invalid(new ValidationError(error));
        }

        // An abandoned import must not hold the form.
        await sweep.ForTenantAsync(request.TenantId, cancellationToken);
        return await CurrentAsync(request, cancellationToken) is { } current ? Conflict(current) : null;
    }

    private async Task<Result<ImportViewDto>> ConflictWithCurrentAsync(
        StartImportCommand request,
        CancellationToken cancellationToken) =>
        await CurrentAsync(request, cancellationToken) is { } winner
            ? Conflict(winner)
            : Result<ImportViewDto>.Conflict(ImportExists);

    private static AudienceImportStartArgs StartArgsOf(StartImportCommand request) =>
        new(
            request.TenantId,
            request.FormId,
            request.FileName,
            request.Headers,
            request.IdentifierColumn,
            request.PropertyColumns,
            request.IsTest,
            request.RowCount,
            ImportRunnerRegistration.ChunkRows,
            request.PerformedById);

    private Task<ImportViewDto> ViewAsync(AudienceImport import, CancellationToken cancellationToken) =>
        ImportView.ReadAsync(new ImportViewSource(db, runner.RunsInBackground), import, cancellationToken);

    private async Task<string?> RequestErrorAsync(StartImportCommand request, CancellationToken cancellationToken)
    {
        ImportLimitsDto limits = ImportRunnerRegistration.LimitsOf(runner);
        if (request.RowCount > limits.MaxRows)
        {
            return $"The file has {request.RowCount:N0} rows. Import up to {limits.MaxRows:N0} at a time.";
        }

        if (!request.Headers.Contains(request.IdentifierColumn, StringComparer.OrdinalIgnoreCase))
        {
            return $"The CSV has no '{request.IdentifierColumn}' column.";
        }

        List<Property> properties = await db.Properties
            .Where(property => property.TenantId == request.TenantId && property.FormId == request.FormId)
            .ToListAsync(cancellationToken);
        return CsvColumnMapper.MappingError(new CsvColumnRequest(
            properties, request.Headers, request.IdentifierColumn, request.PropertyColumns));
    }

    private Task<long?> CurrentAsync(StartImportCommand request, CancellationToken cancellationToken) =>
        Unfinished(db, request.TenantId, request.FormId)
            .Select(import => (long?)import.Id)
            .FirstOrDefaultAsync(cancellationToken);

    internal static IQueryable<AudienceImport> Unfinished(IAudienceDbContext db, long tenantId, long formId) =>
        db.AudienceImports.Where(import => import.TenantId == tenantId
            && import.FormId == formId
            && AudienceImportStatusCodes.Unfinished.Contains(import.Status));

    private static Result<ImportViewDto> Conflict(long currentImportId) =>
        Result<ImportViewDto>.Conflict(
            new ExistingResource(ImportExistsCode, "importId", currentImportId.ToString(System.Globalization.CultureInfo.InvariantCulture)),
            ImportExists);
}
