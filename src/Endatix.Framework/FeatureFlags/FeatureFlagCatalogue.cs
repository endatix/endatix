using System.Collections.Immutable;

namespace Endatix.Framework.FeatureFlags;

/// <summary>
/// The canonical list of Endatix feature flags. One entry per flag, carrying the kebab-case key used by
/// the evaluator alongside the configuration path that seeds it.
/// </summary>
/// <remarks>
/// Adding a flag is an edit here plus a constant in <see cref="FeatureFlags"/>. The Hub keeps its own
/// binding with the same keys and fallback defaults; there is no cross-repo check, because the API is
/// the only evaluator and a key it does not know resolves to the caller's default.
/// </remarks>
public static class FeatureFlagCatalogue
{
    /// <summary>
    /// Every defined flag, in declaration order.
    /// </summary>
    public static readonly ImmutableArray<FeatureFlagDefinition> Definitions =
    [
        // Every flag below is Rollout: none of them is priced yet, so none is an entitlement. The label
        // follows the commercial decision, not the shape of the configuration — reclassifying one needs
        // a deprecation path for installs already relying on it.
        FeatureFlagDefinition.Boolean(
            key: "experimental-features",
            configKey: FeatureFlags.ExperimentalFeatures,
            defaultValue: false,
            flagClass: FeatureFlagClass.Rollout,
            scope: FeatureFlagScope.Tenant),

        FeatureFlagDefinition.Boolean(
            key: "advanced-analytics",
            configKey: FeatureFlags.AdvancedAnalytics,
            defaultValue: false,
            flagClass: FeatureFlagClass.Rollout,
            scope: FeatureFlagScope.Tenant),

        FeatureFlagDefinition.Boolean(
            key: "form-analytics",
            configKey: FeatureFlags.FormAnalytics,
            defaultValue: false,
            flagClass: FeatureFlagClass.Rollout,
            scope: FeatureFlagScope.Tenant),

        FeatureFlagDefinition.Boolean(
            key: "storage-stats",
            configKey: FeatureFlags.StorageStats,
            defaultValue: false,
            flagClass: FeatureFlagClass.Rollout,
            scope: FeatureFlagScope.Tenant),

        FeatureFlagDefinition.Boolean(
            key: "data-lists",
            configKey: FeatureFlags.DataLists,
            defaultValue: false,
            flagClass: FeatureFlagClass.Rollout,
            scope: FeatureFlagScope.Tenant),

        // Deployment-scoped: read at startup by EndatixModuleRegistration.ShouldRegister to decide
        // whether the Reporting module is registered at all. No tenant exists at that point.
        FeatureFlagDefinition.Boolean(
            key: "reporting-module",
            configKey: FeatureFlags.ReportingModule,
            defaultValue: false,
            flagClass: FeatureFlagClass.Rollout,
            scope: FeatureFlagScope.Deployment),
    ];

    private static readonly ImmutableDictionary<string, FeatureFlagDefinition> ByKeyIndex =
        Definitions.ToImmutableDictionary(definition => definition.Key, StringComparer.Ordinal);

    private static readonly ImmutableDictionary<string, FeatureFlagDefinition> ByConfigKeyIndex =
        Definitions.ToImmutableDictionary(definition => definition.ConfigKey, StringComparer.Ordinal);

    /// <summary>
    /// Finds a definition by its kebab-case key, or null when the key is not defined.
    /// </summary>
    public static FeatureFlagDefinition? FindByKey(string key) =>
        string.IsNullOrWhiteSpace(key) ? null : ByKeyIndex.GetValueOrDefault(key);

    /// <summary>
    /// Finds a definition by its configuration path under <c>Endatix:FeatureFlags</c>, or null when the
    /// path does not correspond to a defined flag.
    /// </summary>
    public static FeatureFlagDefinition? FindByConfigKey(string configKey) =>
        string.IsNullOrWhiteSpace(configKey) ? null : ByConfigKeyIndex.GetValueOrDefault(configKey);
}
