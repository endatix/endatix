namespace Endatix.Framework.FeatureFlags;

/// <summary>
/// Resolves whether Data Lists functionality is enabled for the current configuration.
/// </summary>
public interface IFeatureGate
{
    /// <summary>
    /// Returns true when the feature is enabled.
    /// </summary>
    Task<bool> IsEnabledAsync(string featureName, CancellationToken cancellationToken = default);
}
