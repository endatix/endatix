using Endatix.Core.Abstractions.Data;
using Endatix.Core.Abstractions.Repositories;
using Endatix.Core.Entities;
using Endatix.Core.Infrastructure.Paging;
using Microsoft.EntityFrameworkCore;

namespace Endatix.Infrastructure.Data.Repositories;

/// <summary>
/// Repository for a tenant's branding and its versions. A lost race is conceded, not thrown, the way
/// <c>SubmissionViewRepository</c> concedes one.
/// </summary>
/// <remarks>
/// Two writes made from the same version must not both win. The database decides, not a lock: the
/// <see cref="TenantBranding.Version"/> concurrency token makes an update from an older version change no row, and the
/// keys refuse a second first row and a second version with the same number.
/// </remarks>
internal sealed class TenantBrandingRepository(
    AppDbContext dbContext,
    IUniqueConstraintViolationChecker uniqueViolationChecker) : ITenantBrandingRepository
{
    /// <inheritdoc />
    public Task<TenantBranding?> FindAsync(long tenantId, CancellationToken cancellationToken) =>
        dbContext.TenantBrandings
            .FirstOrDefaultAsync(branding => branding.TenantId == tenantId, cancellationToken);

    /// <inheritdoc />
    public Task<TenantBrandingVersion?> FindVersionAsync(long tenantId, long number, CancellationToken cancellationToken) =>
        dbContext.TenantBrandingVersions
            .AsNoTracking()
            .FirstOrDefaultAsync(version => version.TenantId == tenantId && version.Number == number, cancellationToken);

    /// <inheritdoc />
    /// <remarks>
    /// The anonymous public read calls this with the tenant its slug names, and caches the answer for every caller. A
    /// signed-in caller's token still sets the ambient tenant, so the tenant filter would hide another tenant's brand;
    /// the <paramref name="tenantId"/> condition is the isolation here.
    /// </remarks>
    public Task<string?> FindPublishedJsonAsync(long tenantId, CancellationToken cancellationToken) =>
        dbContext.TenantBrandings
            .AsNoTracking()
            .IgnoreQueryFilters([EndatixQueryFilterNames.Tenant])
            .Where(branding => branding.TenantId == tenantId)
            .Select(branding => branding.PublishedJson)
            .FirstOrDefaultAsync(cancellationToken);

    /// <inheritdoc />
    public Task<int> CountVersionsAsync(long tenantId, CancellationToken cancellationToken) =>
        dbContext.TenantBrandingVersions.CountAsync(version => version.TenantId == tenantId, cancellationToken);

    /// <inheritdoc />
    public async Task<IReadOnlyList<TenantBrandingVersion>> ListVersionsAsync(
        long tenantId,
        ResolvedPage page,
        CancellationToken cancellationToken) =>
        await dbContext.TenantBrandingVersions
            .AsNoTracking()
            .Where(version => version.TenantId == tenantId)
            .OrderByDescending(version => version.Number)
            .Skip(page.Skip)
            .Take(page.PageSize)
            .ToListAsync(cancellationToken);

    /// <inheritdoc />
    public async Task<bool> SaveOrConcedeAsync(TenantBranding branding, TenantBrandingVersion version, CancellationToken cancellationToken)
    {
        if (dbContext.Entry(branding).State == EntityState.Detached)
        {
            dbContext.Add(branding);
        }

        dbContext.Add(version);
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
            return true;
        }
        catch (DbUpdateException exception) when (IsLostRace(exception))
        {
            Detach(branding, version);
            return false;
        }
    }

    // A stale Version matches no row; a taken tenant key or (TenantId, Number) is a unique violation.
    private bool IsLostRace(DbUpdateException exception) =>
        exception is DbUpdateConcurrencyException
        || uniqueViolationChecker.AnalyzeUniqueConstraint(exception).IsUniqueConstraintViolation;

    // A refused write leaves nothing tracked, so a later save in the same scope does not replay it
    // (ARCHITECTURE.md, transaction locks, item 8).
    private void Detach(TenantBranding branding, TenantBrandingVersion version)
    {
        dbContext.Entry(branding).State = EntityState.Detached;
        dbContext.Entry(version).State = EntityState.Detached;
    }
}
