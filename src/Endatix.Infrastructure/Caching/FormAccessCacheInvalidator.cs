using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.Logging;

namespace Endatix.Infrastructure.Caching;

internal sealed class FormAccessCacheInvalidator(
    HybridCache hybridCache,
    ILogger<FormAccessCacheInvalidator> logger) : IFormAccessCacheInvalidator
{
    public async Task InvalidateFormAsync(long formId, CancellationToken cancellationToken)
    {
        try
        {
            // ForForm evicts every entry tagged form:{id}, including access/routing entries that also carry ForFormAccess.
            await hybridCache.RemoveByTagAsync([FormAccessCacheTags.ForForm(formId)], cancellationToken);
            logger.LogDebug("Invalidated form access cache for form {FormId}", formId);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "Failed to invalidate cache for form {FormId}. Cache might be stale.", formId);
        }
    }
}
