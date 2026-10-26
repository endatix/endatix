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
/// A form has at most one import it can still change; a second start answers 409 with that import's id. With no
/// <paramref name="Options"/>, every stored value the file does not fill stays.
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
    long? PerformedById,
    AudienceImportOptions? Options = null) : ICommand<Result<ImportViewDto>>;

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
        return await AddAsync(import, cancellationToken)
            ? Result<ImportViewDto>.Created(await ViewAsync(import, cancellationToken))
            : await ConflictWithCurrentAsync(request, cancellationToken);
    }

    /// <summary>
    /// Adds the import. A cancel or a sweep that moved a stopped write first leaves the form free, so the add runs once
    /// more on what is there now; a resume that did leaves an unfinished import, which the second add runs into.
    /// </summary>
    private async Task<bool> AddAsync(AudienceImport import, CancellationToken cancellationToken)
    {
        AddOutcome added = await TryAddAsync(import, cancellationToken);
        if (added == AddOutcome.Moved)
        {
            ((DbContext)db).ChangeTracker.Clear();
            added = await TryAddAsync(import, cancellationToken);
        }

        return added == AddOutcome.Added;
    }

    /// <summary>
    /// Adds the import and, in the same save, expires the form's stopped writes that could still resume: the scripter
    /// chose to start over, so their staging is swept like any expired import's.
    /// </summary>
    private async Task<AddOutcome> TryAddAsync(AudienceImport import, CancellationToken cancellationToken)
    {
        await ExpireStoppedWritesAsync(import, cancellationToken);
        db.AudienceImports.Add(import);
        try
        {
            await db.SaveChangesAsync(cancellationToken);
            return AddOutcome.Added;
        }
        catch (DbUpdateConcurrencyException)
        {
            return AddOutcome.Moved;
        }
        catch (DbUpdateException race)
            when (violations.IsViolationOf(race, AudienceImport.UniqueConstraints.OneUnfinishedPerForm))
        {
            return AddOutcome.Occupied;
        }
    }

    private async Task ExpireStoppedWritesAsync(AudienceImport import, CancellationToken cancellationToken)
    {
        foreach (AudienceImport stopped in await Resumable(db, import.TenantId, import.FormId).ToListAsync(cancellationToken))
        {
            stopped.Expire();
        }
    }

    /// <summary>
    /// How an add ended: added; refused by the unfinished-import index; or a stopped write it expired was moved first.
    /// </summary>
    private enum AddOutcome
    {
        Added,
        Occupied,
        Moved,
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
            request.PerformedById,
            request.Options);

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

    /// <summary>The form's stopped writes that can still resume.</summary>
    internal static IQueryable<AudienceImport> Resumable(IAudienceDbContext db, long tenantId, long formId) =>
        db.AudienceImports.Where(import => import.TenantId == tenantId
            && import.FormId == formId
            && import.Status == AudienceImportStatusCodes.Failed
            && import.Resumable);

    internal static IQueryable<AudienceImport> Unfinished(IAudienceDbContext db, long tenantId, long formId) =>
        db.AudienceImports.Where(import => import.TenantId == tenantId
            && import.FormId == formId
            && AudienceImportStatusCodes.Unfinished.Contains(import.Status));

    private static Result<ImportViewDto> Conflict(long currentImportId) => Conflict<ImportViewDto>(currentImportId);

    /// <summary>The 409 naming the form's unfinished import, for a start or a one-request import.</summary>
    internal static Result<T> Conflict<T>(long currentImportId) =>
        Result<T>.Conflict(
            new ExistingResource(ImportExistsCode, "importId", currentImportId.ToString(System.Globalization.CultureInfo.InvariantCulture)),
            ImportExists);
}
