using Endatix.Framework.Configuration;
using Endatix.Framework.FeatureFlags;
using Microsoft.Extensions.Configuration;

namespace Endatix.Framework.Modules;

/// <summary>
/// Host-configuration helpers for <see cref="IEndatixModule"/> registration.
/// </summary>
public static class EndatixModuleRegistration
{
    /// <summary>
    /// Returns whether the module should be registered given current host configuration.
    /// </summary>
    public static bool ShouldRegister(IConfiguration configuration, IEndatixModule module)
    {
        if (module is not IHasFeatureFlag featureFlagModule)
        {
            return true;
        }

        return IsFeatureFlagEnabled(configuration, featureFlagModule.FeatureFlag);
    }

    /// <summary>
    /// Returns whether the named feature flag is enabled in <c>Endatix:FeatureFlags</c>.
    /// </summary>
    public static bool IsFeatureFlagEnabled(IConfiguration configuration, string featureName)
    {
        if (string.IsNullOrWhiteSpace(featureName))
        {
            return false;
        }

        var sectionPath = $"{EndatixOptionsBase.RootSectionName}:{FeatureFlagsOptions.RelativeSectionPath}";
        var flagValue = configuration.GetSection(sectionPath)[featureName];

        if (string.IsNullOrWhiteSpace(flagValue))
        {
            return false;
        }

        return bool.TryParse(flagValue, out var enabled) && enabled;
    }
}
