using System.Text.Json;
using Endatix.Core.Infrastructure.Messaging;
using Endatix.Core.Infrastructure.Paging;
using Endatix.Core.Infrastructure.Result;
using Endatix.Modules.Audience.Persistence;
using Endatix.Modules.Audience.Shared;
using Microsoft.EntityFrameworkCore;

namespace Endatix.Modules.Audience.Features.Imports;

/// <summary>
/// Every row an import did not write, by spreadsheet row, with its cells as uploaded, for as long as the import keeps
/// them (7 days after it finished, see <see cref="ImportSweep"/>). The import view shows only the first 50.
/// </summary>
public sealed record ListImportRejectionsQuery(long TenantId, long FormId, long ImportId, int? Page, int? PageSize)
    : IQuery<Result<Paged<ImportRejectedRowDto>>>;

/// <summary>One rejected row: its spreadsheet row, why it was not written, and its cells in header order.</summary>
public sealed record ImportRejectedRowDto(int RowNumber, string Reason, IReadOnlyList<string> Cells);

internal sealed class ListImportRejectionsHandler(IAudienceDbContext db)
    : IQueryHandler<ListImportRejectionsQuery, Result<Paged<ImportRejectedRowDto>>>
{
    public async Task<Result<Paged<ImportRejectedRowDto>>> Handle(
        ListImportRejectionsQuery request,
        CancellationToken cancellationToken)
    {
        if (request.TenantId <= 0)
        {
            return Result<Paged<ImportRejectedRowDto>>.Unauthorized("Tenant context is required.");
        }

        bool found = await db.AudienceImports.AnyAsync(
            import => import.Id == request.ImportId
                && import.TenantId == request.TenantId
                && import.FormId == request.FormId,
            cancellationToken);
        return found
            ? Result.Success(await PageAsync(request, cancellationToken))
            : Result<Paged<ImportRejectedRowDto>>.NotFound("Import not found.");
    }

    private async Task<Paged<ImportRejectedRowDto>> PageAsync(
        ListImportRejectionsQuery request,
        CancellationToken cancellationToken)
    {
        var rejections = db.ImportRejections.AsNoTracking()
            .Where(rejection => rejection.TenantId == request.TenantId && rejection.ImportId == request.ImportId);
        ResolvedPage resolved = ResolvedPage.For(
            request.Page ?? 1,
            request.PageSize ?? AudiencePaging.DefaultPageSize,
            await rejections.CountAsync(cancellationToken));
        var rows = await rejections
            .OrderBy(rejection => rejection.RowNumber)
            .Skip(resolved.Skip)
            .Take(resolved.PageSize)
            .Select(rejection => new { rejection.RowNumber, rejection.Reason, rejection.CellsJson })
            .ToListAsync(cancellationToken);
        return resolved.ToPaged<ImportRejectedRowDto>(
            [.. rows.Select(row => new ImportRejectedRowDto(row.RowNumber, row.Reason, CellsOf(row.CellsJson)))]);
    }

    private static List<string> CellsOf(string cellsJson) => JsonSerializer.Deserialize<List<string>>(cellsJson) ?? [];
}
