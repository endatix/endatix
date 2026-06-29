namespace Endatix.Infrastructure.Caching;

/// <summary>
/// Invalidates HybridCache entries associated with a form.
/// </summary>
public interface IFormAccessCacheInvalidator
{
    /// <summary>
    /// Removes all cache entries tagged for the given form.
    /// </summary>
    Task InvalidateFormAsync(long formId, CancellationToken cancellationToken);
}
