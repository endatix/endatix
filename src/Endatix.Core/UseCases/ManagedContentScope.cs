using System.Diagnostics.CodeAnalysis;
using Ardalis.Specification;
using Endatix.Core.Abstractions;
using Endatix.Core.Abstractions.Authorization;
using Endatix.Core.Entities;
using Endatix.Core.Infrastructure.Domain;
using Endatix.Core.Infrastructure.Result;

namespace Endatix.Core.UseCases;

/// <summary>The tenant and account decision for managed content operations.</summary>
[Experimental("ENDATIX0006")]
public sealed class ManagedContentScope(ITenantContext tenant, AccountStanding standing)
{
    internal long TenantId => tenant.TenantId;
    internal bool IsEnforced => standing.IsEnforced;

    internal Task<Result<T>?> RefusalAsync<T>(CancellationToken ct) => TenantId > 0
        ? standing.RefusalAsync<T>(ct)
        : Task.FromResult<Result<T>?>(Result.Unauthorized("Tenant context is required."));

    internal async Task<Result<TResult>?> TargetRefusalAsync<T, TResult>(IRepository<T> rows, ContentTarget target, CancellationToken ct) where T : TenantEntity, IAggregateRoot
    {
        if (!IsEnforced)
        {
            return null;
        }
        return TenantId > 0 && await rows.AnyAsync(ById<T>(target.Id), ct)
            ? await standing.RefusalAsync<TResult>(ct) : Result.NotFound(target.NotFound);
    }

    internal Specification<T> ById<T>(long id) where T : TenantEntity =>
        new TenantTarget<T>(id, TenantId);

    private sealed class TenantTarget<T> : Specification<T> where T : TenantEntity
    {
        internal TenantTarget(long id, long tenantId) =>
            Query.Where(row => tenantId > 0 && row.TenantId == tenantId && row.Id == id);
    }
}

internal static class ManagedContentQueries
{
    internal static Task<T?> FindAsync<T>(this IRepository<T> rows, long id, ManagedContentScope? scope,
        CancellationToken ct) where T : TenantEntity, IAggregateRoot =>
        scope is null ? rows.GetByIdAsync(id, ct) : rows.FirstOrDefaultAsync(scope.ById<T>(id), ct);

    internal static void InTenant<T>(this Specification<T> spec, ManagedContentScope? scope) where T : TenantEntity
    {
        if (scope is not null)
        {
            var tenant = scope.TenantId;
            spec.Query.Where(row => tenant > 0 && row.TenantId == tenant);
        }
    }
}

internal sealed record ContentTarget(long Id, string NotFound);
