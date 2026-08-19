namespace Endatix.Framework.FeatureFlags;

/// <summary>
/// Feature flag keys for the Feature Flags system.
/// </summary>
/// <remarks>
/// These values double as the configuration paths under <c>Endatix:FeatureFlags</c> — both
/// <see cref="Modules.EndatixModuleRegistration.IsFeatureFlagEnabled"/> and Microsoft.FeatureManagement
/// resolve a flag by looking its name up in that section. They are therefore the
/// <see cref="FeatureFlagDefinition.ConfigKey"/> of each entry in <see cref="FeatureFlagCatalogue"/>,
/// and must not be changed: an install setting <c>Endatix:FeatureFlags:ReportingModule</c> would
/// silently fall back to the default. The kebab-case evaluator keys live in the catalogue instead, and
/// these constants adopt them only once nothing resolves configuration by flag name.
/// </remarks>
public static class FeatureFlags
{

    /// <summary>
    /// Feature flag key for enabling Experimental Features.
    /// </summary>
    public const string ExperimentalFeatures = "ExperimentalFeatures";

    /// <summary>
    /// Feature flag key for enabling Advanced Analytics.
    /// </summary>
    public const string AdvancedAnalytics = "AdvancedAnalytics";

    /// <summary>
    /// Feature flag key for enabling Form Analytics.
    /// </summary>
    public const string FormAnalytics = "FormAnalytics";

    /// <summary>
    /// Feature flag key for enabling Storage Stats.
    /// </summary>
    public const string StorageStats = "StorageStats";

    /// <summary>
    /// Feature flag key for enabling Data Lists.
    /// </summary>
    public const string DataLists = "DataLists";

    /// <summary>
    /// Feature flag key for enabling the Reporting module (endpoints, DbContext, migrations).
    /// </summary>
    public const string ReportingModule = "ReportingModule";
}