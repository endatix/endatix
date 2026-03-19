using Endatix.Core.Authorization.Access.Contracts;

namespace Endatix.Infrastructure.Features.AccessControl.Contracts;

public static class ResourceAccessQueryExtensions
{
    public static async Task<bool> CanPerformAsync<TData, TContext>(
        this IResourceAccessQuery<TData, TContext> query,
        TContext context,
        string permission,
        CancellationToken ct = default)
        where TData : class, IAccessData
        where TContext : class
    {
        var result = await query.GetAccessData(context, ct);
        return result.IsSuccess && result.Value.Data.Has(permission);
    }

    public static async Task<bool> CanPerformAnyAsync<TData, TContext>(
        this IResourceAccessQuery<TData, TContext> query,
        TContext context,
        IEnumerable<string> permissions,
        CancellationToken ct = default)
        where TData : class, IAccessData
        where TContext : class
    {
        var result = await query.GetAccessData(context, ct);
        return result.IsSuccess && result.Value.Data.HasAny(permissions);
    }

    public static async Task<bool> CanPerformAllAsync<TData, TContext>(
        this IResourceAccessQuery<TData, TContext> query,
        TContext context,
        IEnumerable<string> permissions,
        CancellationToken ct = default)
        where TData : class, IAccessData
        where TContext : class
    {
        var result = await query.GetAccessData(context, ct);
        return result.IsSuccess && result.Value.Data.HasAll(permissions);
    }
}
