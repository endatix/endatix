using Ardalis.GuardClauses;
using Endatix.Framework.FeatureFlags;
using FastEndpoints;

namespace Endatix.Api.Common.FeatureFlags;

/// <summary>
/// Feature flag for an endpoint that is used to check if the endpoint is enabled.
/// </summary>
public class EndpointFeatureGate : IFeatureFlag
{
    /// <inheritdoc />
    public string? Name { get; set; }

    /// <inheritdoc />
    public async Task<bool> IsEnabledAsync(IEndpoint endpoint)
    {
        Guard.Against.NullOrWhiteSpace(Name);

        var scopedService = endpoint.HttpContext.Resolve<IFeatureGate>();

        return await scopedService.IsEnabledAsync(Name, endpoint.HttpContext.RequestAborted);
    }
}