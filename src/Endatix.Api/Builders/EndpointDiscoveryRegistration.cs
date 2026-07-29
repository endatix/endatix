using System.Reflection;
using FastEndpoints;
using Microsoft.Extensions.DependencyInjection;

namespace Endatix.Api.Builders;

/// <summary>
/// Owns the set of assemblies FastEndpoints discovers endpoints from, and the single registration
/// that set produces.
/// </summary>
/// <remarks>
/// <para>
/// The state belongs to the <see cref="IServiceCollection"/> rather than to a builder instance:
/// <c>AddApiEndpoints</c> and <c>GetApiBuilder</c> each hand out a fresh
/// <see cref="ApiConfigurationBuilder"/> over the same services, and <c>AddFastEndpoints</c> is
/// last-registration-wins. With the set held per builder, a second builder would re-register with
/// only its own assemblies and silently drop the first builder's endpoints.
/// </para>
/// <para>
/// Registration is replaced rather than repeated. <c>AddFastEndpoints</c> registers
/// <c>EndpointData</c>, <c>CommandHandlerRegistry</c>, <c>EventBus&lt;&gt;</c> and <c>Cfg</c> with
/// <c>AddSingleton</c> rather than <c>TryAdd</c>, so calling it repeatedly leaves a full set of
/// descriptors behind each time — and discovery is eager, so every call also re-runs a reflection
/// pass over every accumulated assembly.
/// </para>
/// </remarks>
internal sealed class EndpointDiscoveryRegistration
{
    private readonly List<Assembly> _assemblies = [];
    private readonly List<ServiceDescriptor> _ownedDescriptors = [];

    /// <summary>
    /// The assemblies accumulated so far, in the order they were added.
    /// </summary>
    public IReadOnlyList<Assembly> Assemblies => _assemblies;

    /// <summary>
    /// Whether FastEndpoints has been registered at least once.
    /// </summary>
    public bool IsRegistered => _ownedDescriptors.Count > 0;

    /// <summary>
    /// Returns the registration attached to <paramref name="services"/>, attaching a new one on
    /// first use. Held as a singleton instance descriptor so every builder over the same service
    /// collection shares it.
    /// </summary>
    public static EndpointDiscoveryRegistration GetOrAdd(IServiceCollection services)
    {
        var existing = services
            .FirstOrDefault(descriptor => descriptor.ServiceType == typeof(EndpointDiscoveryRegistration))?
            .ImplementationInstance;

        if (existing is EndpointDiscoveryRegistration registration)
        {
            return registration;
        }

        registration = new EndpointDiscoveryRegistration();
        services.AddSingleton(registration);
        return registration;
    }

    /// <summary>
    /// Adds assemblies to the discovery set, ignoring duplicates.
    /// </summary>
    /// <returns><c>true</c> when at least one assembly was new.</returns>
    public bool Add(IEnumerable<Assembly> assemblies)
    {
        var added = false;

        foreach (var assembly in assemblies)
        {
            if (_assemblies.Contains(assembly))
            {
                continue;
            }

            _assemblies.Add(assembly);
            added = true;
        }

        return added;
    }

    /// <summary>
    /// Registers FastEndpoints for the accumulated assemblies, first removing the descriptors a
    /// previous call added so the container holds exactly one live registration.
    /// </summary>
    public void Register(IServiceCollection services)
    {
        foreach (var descriptor in _ownedDescriptors)
        {
            services.Remove(descriptor);
        }

        _ownedDescriptors.Clear();

        // AddFastEndpoints only appends, so everything past this index is ours to own and to
        // remove again on the next call. Descriptors are tracked by reference rather than by index
        // because removing one shifts the rest.
        var firstOwnedIndex = services.Count;
        Assembly[] assemblies = [.. _assemblies];

        services.AddFastEndpoints(options =>
        {
            options.DisableAutoDiscovery = true;
            options.Assemblies = assemblies;
        });

        _ownedDescriptors.AddRange(services.Skip(firstOwnedIndex));
    }
}
