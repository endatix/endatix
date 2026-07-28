using FastEndpoints;

namespace Endatix.Api.Infrastructure;

/// <summary>
/// Optional capability for an <c>IEndatixModule</c> that needs to contribute FastEndpoints
/// configuration — serializers, OpenAPI tags, and similar endpoint-level settings.
/// </summary>
/// <remarks>
/// Applied by <c>EndatixBuilder.UseModule</c> only when the module is actually registered, so a
/// module gated off by its feature flag contributes nothing. Mirrors the existing
/// <c>IHasFeatureFlag</c> / <c>IHasDbMigrations</c> capabilities, which live in
/// <c>Endatix.Framework</c>. This one lives in <c>Endatix.Api</c> instead because
/// <c>Endatix.Framework</c> has no FastEndpoints dependency and must not gain one.
/// </remarks>
public interface IHasFastEndpointsConfig
{
    /// <summary>
    /// Applies the module's FastEndpoints configuration.
    /// </summary>
    /// <param name="config">The FastEndpoints configuration to mutate.</param>
    void ConfigureFastEndpoints(Config config);
}
