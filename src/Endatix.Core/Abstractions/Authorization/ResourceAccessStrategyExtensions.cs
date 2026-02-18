namespace Endatix.Core.Abstractions.Authorization;

public static class ResourceAccessStrategyExtensions
{
    public static async Task<bool> CanPerformAsync<TData, TContext>(
        this IResourceAccessStrategy<TData, TContext> strategy,
        TContext context,
        string permission,
        CancellationToken ct = default)
        where TData : class, IAccessData
        where TContext : class
    {
        var result = await strategy.GetAccessData(context, ct);
        return result.IsSuccess && result.Value.Data.Has(permission);
    }

    public static async Task<bool> CanPerformAnyAsync<TData, TContext>(
        this IResourceAccessStrategy<TData, TContext> strategy,
        TContext context,
        IEnumerable<string> permissions,
        CancellationToken ct = default)
        where TData : class, IAccessData
        where TContext : class
    {
        var result = await strategy.GetAccessData(context, ct);
        return result.IsSuccess && result.Value.Data.HasAny(permissions);
    }

    public static async Task<bool> CanPerformAllAsync<TData, TContext>(
        this IResourceAccessStrategy<TData, TContext> strategy,
        TContext context,
        IEnumerable<string> permissions,
        CancellationToken ct = default)
        where TData : class, IAccessData
        where TContext : class
    {
        var result = await strategy.GetAccessData(context, ct);
        return result.IsSuccess && result.Value.Data.HasAll(permissions);
    }
}
