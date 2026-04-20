using Microsoft.Extensions.Logging;
using Microsoft.FeatureManagement;

namespace Endatix.Framework.FeatureFlags;

internal sealed class FeatureGate(IFeatureManager featureManager, ILogger<FeatureGate> logger) : IFeatureGate
{
    /// <inheritdoc />
    public async Task<bool> IsEnabledAsync(string featureName, CancellationToken cancellationToken = default)
    {
        try
        {
            return await featureManager.IsEnabledAsync(featureName, cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error checking feature flag {FeatureName}. Failing closed (feature disabled).", featureName);
            return false;
        }
    }
}
