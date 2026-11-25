using Endatix.Framework.Modules;
using FastEndpoints;
using Microsoft.Extensions.Configuration;
using Flags = Endatix.Framework.FeatureFlags.FeatureFlags;

namespace Endatix.Api.Common.FeatureFlags;

/// <summary>
/// The endpoints the WhiteLabeling flag gates. When it is off they answer 404, and the API docs leave them out.
/// </summary>
/// <remarks>
/// The flag is deployment-scoped. The docs read it once at startup, and the endpoints stay registered, so the 404
/// comes from the per-request gate.
/// </remarks>
internal static class WhiteLabelingEndpoints
{
    /// <summary>Gates the endpoint on WhiteLabeling and tags it, so the API docs can leave it out.</summary>
    internal static void GateOnWhiteLabeling(this EndpointDefinition definition)
    {
        definition.FeatureFlag<EndpointFeatureGate>(Flags.WhiteLabeling);
        definition.Tags(Flags.WhiteLabeling);
    }

    /// <summary>Whether the deployment has WhiteLabeling on. Without a configuration, true: nothing is hidden.</summary>
    internal static bool IsEnabled(IConfiguration? configuration) =>
        configuration is null || EndatixModuleRegistration.IsFeatureFlagEnabled(configuration, Flags.WhiteLabeling);

    /// <summary>Whether the API docs show the endpoint: not when WhiteLabeling gates it and is off.</summary>
    internal static bool IsDocumented(EndpointDefinition endpoint, bool whiteLabelingEnabled) =>
        whiteLabelingEnabled || endpoint.EndpointTags?.Contains(Flags.WhiteLabeling) is not true;
}
