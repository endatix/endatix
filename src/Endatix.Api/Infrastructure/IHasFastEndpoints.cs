using System.Reflection;
using FastEndpoints;

namespace Endatix.Api.Infrastructure;

/// <summary>
/// Optional capability for an <c>IEndatixModule</c> that contributes FastEndpoints endpoints, and
/// optionally the endpoint-level configuration they need — serializers, OpenAPI tags and similar.
/// </summary>
/// <remarks>
/// <para>
/// <c>EndatixBuilder.UseModule</c> registers endpoint discovery only for modules implementing this
/// interface, so a module shipping handlers but no endpoints costs nothing at startup. A module
/// gated off by its feature flag contributes nothing either — <c>UseModule</c> returns before it
/// gets this far.
/// </para>
/// <para>
/// Mirrors the <c>IHasFeatureFlag</c> and <c>IHasDbMigrations</c> capabilities, which live in
/// <c>Endatix.Framework</c>. This one lives in <c>Endatix.Api</c> instead because
/// <c>Endatix.Framework</c> has no FastEndpoints dependency and must not gain one.
/// </para>
/// </remarks>
public interface IHasFastEndpoints
{
    /// <summary>
    /// The assemblies FastEndpoints scans for this module's endpoints. Defaults to the assembly
    /// declaring the module, which is where a module's endpoints normally live — override only
    /// when they ship in a separate assembly.
    /// </summary>
    IEnumerable<Assembly> EndpointAssemblies => [GetType().Assembly];

    /// <summary>
    /// Applies the module's FastEndpoints configuration. The default is a no-op: implement this
    /// only when the module needs its own serializers, OpenAPI tags or similar endpoint settings.
    /// </summary>
    /// <param name="config">The FastEndpoints configuration to mutate.</param>
    void ConfigureFastEndpoints(Config config) { }
}
