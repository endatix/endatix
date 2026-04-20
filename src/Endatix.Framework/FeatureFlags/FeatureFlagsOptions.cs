using Endatix.Framework.Configuration;

namespace Endatix.Framework.FeatureFlags;

/// <summary>
/// Configuration for Data Lists (JSON-backed choice sources). Feature state is evaluated via
/// <see cref="Microsoft.FeatureManagement.IFeatureManager"/> using the same configuration section.
/// </summary>
public sealed class FeatureFlagsOptions : EndatixOptionsBase
{
    /// <summary>
    /// Relative path under <see cref="EndatixOptionsBase.RootSectionName"/> used for options binding and feature definitions.
    /// </summary>
    public const string RelativeSectionPath = "FeatureFlags";

    /// <inheritdoc />
    public override string SectionPath => RelativeSectionPath;
}
