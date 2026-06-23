using Endatix.Framework.Configuration;
using Endatix.Framework.FeatureFlags;
using Microsoft.Extensions.Configuration;

namespace Endatix.Framework.Modules;

/// <summary>
/// Host-configuration helpers for <see cref="IEndatixModule"/> registration.
/// </summary>
internal static class EndatixModuleRegistration
{
    internal static bool ShouldRegister(IConfiguration configuration, IEndatixModule module)
    {
        if (module is not IHasFeatureFlag featureFlagModule)
        {
            return true;
        }

        return IsFeatureFlagEnabled(configuration, featureFlagModule.FeatureFlag);
    }

    internal static bool IsFeatureFlagEnabled(IConfiguration configuration, string featureName)
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
