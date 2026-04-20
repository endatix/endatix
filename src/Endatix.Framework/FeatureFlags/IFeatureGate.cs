namespace Endatix.Framework.FeatureFlags;

/// <summary>
/// Provides a way to check if a feature is enabled.
/// </summary>
public interface IFeatureGate
{
    /// <summary>
    /// Returns true when the feature is enabled.
    /// </summary>
    Task<bool> IsEnabledAsync(string featureName, CancellationToken cancellationToken = default);
}
