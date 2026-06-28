using Microsoft.FeatureManagement;

namespace Endatix.Infrastructure.FeatureFlags;

/// <summary>
/// Infrastructure extensions for Microsoft Feature Management registration.
/// </summary>
public static class FeatureFlagsInfrastructureExtensions
{
    /// <summary>
    /// Adds tenant/user targeting for Endatix feature flags (requires Core context services).
    /// </summary>
    public static IFeatureManagementBuilder WithEndatixTargeting(this IFeatureManagementBuilder builder)
    {
        return builder.WithTargeting<FeatureFlagsTargetingContext>();
    }
}
