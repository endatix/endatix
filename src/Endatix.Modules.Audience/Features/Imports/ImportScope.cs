using Endatix.Core.Abstractions.Data;
using Endatix.Core.Entities;
using Endatix.Core.Infrastructure.Domain;
using Endatix.Core.Infrastructure.Result;
using Endatix.Infrastructure.Data;
using Endatix.Modules.Audience.Domain;
using Endatix.Modules.Audience.Features.Forms;
using Endatix.Modules.Audience.Features.Import;
using Endatix.Modules.Audience.Features.People;
using Endatix.Modules.Audience.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Endatix.Modules.Audience.Features.Imports;

/// <summary>The services one part of a run uses, from that part's own scope.</summary>
internal sealed class ImportScope
{
    private ImportScope(IServiceProvider services)
    {
        Db = services.GetRequiredService<IAudienceDbContext>();
        Forms = services.GetRequiredService<IRepository<Form>>();
        Support = services.GetRequiredService<ImportWriteSupport>();
        MatchKeyLock = services.GetRequiredService<MatchKeyLock>();
    }

    public IAudienceDbContext Db { get; }

    public IRepository<Form> Forms { get; }

    public ImportWriteSupport Support { get; }

    public MatchKeyLock MatchKeyLock { get; }

    public static ImportScope Of(AsyncServiceScope scope) => new(scope.ServiceProvider);

    public static bool IsAudienceRace(IServiceScopeFactory scopes, DbUpdateException exception)
    {
        using IServiceScope scope = scopes.CreateScope();
        IUniqueConstraintViolationChecker violations =
            scope.ServiceProvider.GetRequiredService<ImportWriteSupport>().Violations;
        return violations.IsViolationOf(exception, Member.UniqueConstraints.IdentifierPerTenant)
            || violations.IsViolationOf(exception, Membership.UniqueConstraints.MemberPerForm)
            || violations.IsViolationOf(exception, PropertyValue.UniqueConstraints.CellPerMembership)
            || DatabaseDeadlocks.IsVictim(exception);
    }

    public ImportMatchKey MatchKey(string identifierKind) => new(identifierKind, Support.Normalizer);

    public Task<AudienceImport?> LoadAsync(ImportRef target, CancellationToken cancellationToken) =>
        Db.AudienceImports.SingleOrDefaultAsync(
            import => import.Id == target.ImportId
                && import.TenantId == target.TenantId
                && import.FormId == target.FormId,
            cancellationToken);

    public async Task<bool> FormExistsAsync(ImportRef target, CancellationToken cancellationToken)
    {
        Form? form = await Forms.GetByIdAsync(target.FormId, cancellationToken);
        return form is not null && form.TenantId == target.TenantId;
    }

    /// <summary>The import's column mapping against the form's Properties as they are now.</summary>
    public async Task<Result<CsvColumnMap>> MapAsync(AudienceImport import, CancellationToken cancellationToken)
    {
        List<Property> properties = await Db.Properties
            .Where(property => property.TenantId == import.TenantId && property.FormId == import.FormId)
            .ToListAsync(cancellationToken);
        CsvColumnRequest columns = new(properties, import.Headers, import.IdentifierColumn!, import.PropertyColumns);
        return CsvColumnMapper.MappingError(columns) is null
            ? Result.Success(CsvColumnMapper.Build(columns))
            : Result<CsvColumnMap>.Invalid(new ValidationError(ImportRun.MappingGone));
    }

    /// <summary>Whether the staged chunks are exactly the declared file: unique row numbers, and that many of them.</summary>
    public async Task<bool> StagedRowsAreTheFileAsync(AudienceImport import, CancellationToken cancellationToken)
    {
        List<ChunkSpan> spans = await Db.ImportChunks.AsNoTracking()
            .Where(chunk => chunk.TenantId == import.TenantId && chunk.ImportId == import.Id)
            .Select(chunk => new ChunkSpan(chunk.ChunkIndex, chunk.FirstRowNumber, chunk.RowCount))
            .ToListAsync(cancellationToken);
        return StagedChunkRanges.IsTheFile(import.RowCount, spans);
    }

    public async Task<IReadOnlyList<NumberedRow>> ChunkRowsAsync(
        ImportRef target,
        int index,
        CancellationToken cancellationToken)
    {
        AudienceImportChunk? chunk = await Db.ImportChunks.AsNoTracking().SingleOrDefaultAsync(
            chunk => chunk.TenantId == target.TenantId && chunk.ImportId == target.ImportId && chunk.ChunkIndex == index,
            cancellationToken);
        return chunk?.Rows() ?? [];
    }

    /// <summary>The next <paramref name="count"/> staged rows after the import's checkpoint, in row order.</summary>
    public async Task<IReadOnlyList<NumberedRow>> RowsAfterAsync(
        AudienceImport import,
        int count,
        CancellationToken cancellationToken)
    {
        int after = import.WrittenThroughRow;
        IQueryable<AudienceImportChunk> staged = Db.ImportChunks.AsNoTracking()
            .Where(chunk => chunk.TenantId == import.TenantId && chunk.ImportId == import.Id);
        int from = await ChunkHoldingAsync(staged, after, cancellationToken);
        List<AudienceImportChunk> chunks = await staged
            .Where(chunk => chunk.ChunkIndex >= from)
            .OrderBy(chunk => chunk.ChunkIndex)
            .Take(1 + (count / Math.Max(1, import.ChunkSize)) + 1)
            .ToListAsync(cancellationToken);
        return chunks
            .SelectMany(chunk => chunk.Rows())
            .Where(row => row.RowNumber > after)
            .Take(count)
            .ToList();
    }

    /// <summary>
    /// The chunk that holds row <paramref name="row"/>, or the first: row numbers may skip, so a chunk's last row is not
    /// known before it is read.
    /// </summary>
    private static async Task<int> ChunkHoldingAsync(
        IQueryable<AudienceImportChunk> staged,
        int row,
        CancellationToken cancellationToken) =>
        await staged
            .Where(chunk => chunk.FirstRowNumber <= row)
            .MaxAsync(chunk => (int?)chunk.ChunkIndex, cancellationToken) ?? 0;

    public async Task<HashSet<int>> RejectedRowsAsync(
        AudienceImport import,
        IReadOnlyList<NumberedRow> batch,
        CancellationToken cancellationToken)
    {
        int first = batch[0].RowNumber;
        int last = batch[^1].RowNumber;
        List<int> rows = await Db.ImportRejections
            .Where(rejection => rejection.TenantId == import.TenantId
                && rejection.ImportId == import.Id
                && rejection.RowNumber >= first
                && rejection.RowNumber <= last)
            .Select(rejection => rejection.RowNumber)
            .ToListAsync(cancellationToken);
        return [.. rows];
    }

    public void AddRejections(
        AudienceImport import,
        IReadOnlyList<NumberedRow> rows,
        IReadOnlyList<ImportRejectionDto> rejected)
    {
        Dictionary<int, NumberedRow> byNumber = rows.ToDictionary(row => row.RowNumber);
        foreach (ImportRejectionDto rejection in rejected)
        {
            Db.ImportRejections.Add(AudienceImportRejection.Create(import, byNumber[rejection.RowNumber], rejection.Reason));
        }
    }

    /// <summary>Sorts one chunk's rows, stages its rejections, and counts who is new to the form.</summary>
    public async Task<CheckedRows> ReadChunkAsync(
        AudienceImport import,
        ChunkCheck check,
        CancellationToken cancellationToken)
    {
        IReadOnlyList<NumberedRow> rows = await ChunkRowsAsync(check.Target, check.Index, cancellationToken);
        ImportRows read = check.Reader.ReadPart(rows.Select(row => ImportRun.ToCsvRow(row, check.Headers)));
        ExistingAudience existing = await ExistingAsync(import, read.Accepted, cancellationToken);
        int onForm = read.Accepted.Count(existing.IsOnForm);
        AddRejections(import, rows, read.Rejected);
        return new CheckedRows(rows.Count, read.Accepted.Count - onForm, onForm, read.Rejected.Count);
    }

    /// <summary>The Participants, memberships and cells the rows already match.</summary>
    public Task<ExistingAudience> ExistingAsync(
        AudienceImport import,
        IReadOnlyList<ImportRow> rows,
        CancellationToken cancellationToken) =>
        ExistingAudience.LoadAsync(new AudienceQuery(Db, TargetOf(import), cancellationToken), rows);

    /// <summary>Stages the rows' Participants, clearing what the import's options clear.</summary>
    public async Task<ImportTally> StageAsync(AcceptedRows accepted, CancellationToken cancellationToken)
    {
        (AudienceImport import, CsvColumnMap map, IReadOnlyList<ImportRow> rows) = accepted;
        ExistingAudience existing = await ExistingAsync(import, rows, cancellationToken);
        MatchKey? matchKey = MatchKeyProperty.In(map.PropertiesById.Values, import.IdentifierKind!);
        ImportTarget target = TargetOf(import) with { Clears = ImportClears.Of(map, import.Options, matchKey) };
        ImportBatch batch = new(Db, target, existing);
        foreach (ImportRow row in rows)
        {
            batch.Stage(row);
        }

        return batch.Tally;
    }

    public Task DeleteChunksAsync(AudienceImport import, CancellationToken cancellationToken) =>
        Db.ImportChunks
            .Where(chunk => chunk.TenantId == import.TenantId && chunk.ImportId == import.Id)
            .ExecuteDeleteAsync(cancellationToken);

    private ImportTarget TargetOf(AudienceImport import) =>
        new(import.TenantId, import.FormId, MatchKey(import.IdentifierKind!), import.IsTest);

    /// <summary>Saves, unless another writer moved the import first; false when it did.</summary>
    public async Task<bool> SaveUnlessMovedAsync(CancellationToken cancellationToken)
    {
        try
        {
            await Db.SaveChangesAsync(cancellationToken);
            return true;
        }
        catch (DbUpdateConcurrencyException)
        {
            return false;
        }
    }

    /// <summary>Ends the import Failed with <paramref name="failure"/>'s message and returns the failure.</summary>
    public async Task<Result> FailAsync(AudienceImport import, IResult failure, CancellationToken cancellationToken)
    {
        string reason = failure.Errors.FirstOrDefault()
            ?? failure.ValidationErrors.FirstOrDefault()?.ErrorMessage
            ?? "The import could not continue.";
        import.Fail(reason);
        await SaveUnlessMovedAsync(cancellationToken);
        if (reason == ImportRun.FormDeleted)
        {
            // The form's cleanup may have run before this import's last batch committed.
            await FormAudienceCleanup.RunAsync(Db, new(import.TenantId, import.FormId), cancellationToken);
        }

        return Result.Invalid(new ValidationError(reason));
    }
}

/// <summary>One write batch's accepted rows, and the mapping they were read with.</summary>
internal sealed record AcceptedRows(AudienceImport Import, CsvColumnMap Map, IReadOnlyList<ImportRow> Rows);

/// <summary>One chunk of a Check, and the reader the whole Check shares.</summary>
internal sealed record ChunkCheck(
    ImportRef Target,
    int Index,
    IReadOnlyList<string> Headers,
    ImportRowReader.RowSorter Reader);
