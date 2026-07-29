using Endatix.Infrastructure.FeatureFlags;
using Endatix.Outbox.Engine;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using OpenFeature;
using OpenFeature.Hosting;
using OpenFeature.Hosting.Providers.Memory;
using OpenFeature.Providers.Memory;

namespace Endatix.Infrastructure.Features.Outbox;

/// <summary>
/// Endatix-side wiring for the <c>Endatix.Outbox.Engine</c> relay (Stage 1: in-process, webhook delivery, no
/// DAPR). Registers the engine relay loop, binds <see cref="OutboxOptions"/> from <c>Endatix:Outbox</c>, the
/// composite integration-event publisher (webhooks + module subscribers), and the OpenFeature gate provider. The per-provider claim store
/// (<c>AddSqlOutboxClaimStore</c>) is registered by the active persistence builder, because the dialect and
/// connection type are provider-specific.
/// </summary>
public static class OutboxRelayServiceCollectionExtensions
{
    /// <summary>
    /// Registers the in-process outbox relay: the engine loop + gate, <see cref="OutboxOptions"/> bound from
    /// the <c>Endatix:Outbox</c> config section, the composite <see cref="IIntegrationEventPublisher"/>, and the
    /// OpenFeature provider seeding the <c>outbox-relay-in-process</c> flag.
    /// Safe to call multiple times (e.g. once per DbContext persistence registration).
    /// </summary>
    public static IServiceCollection AddEndatixOutboxRelay(this IServiceCollection services)
    {
        var relayAlreadyRegistered = services.Any(descriptor =>
            descriptor.ServiceType == typeof(IHostedService)
            && descriptor.ImplementationType == typeof(OutboxRelayBackgroundService));

        if (relayAlreadyRegistered)
        {
            return services;
        }

        services.AddOutboxRelay();
        services.AddOptions<OutboxOptions>().BindConfiguration("Endatix:Outbox");
        services.AddScoped<IOutboxIntegrationEventHandler, WebHookOutboxIntegrationEventHandler>();
        services.AddScoped<IIntegrationEventPublisher, CompositeIntegrationEventPublisher>();
        services.AddEndatixOpenFeature();

        return services;
    }

    /// <summary>
    /// Registers an OpenFeature in-memory provider that seeds <see cref="OutboxFlags.RelayInProcess"/> from
    /// <c>Endatix:Outbox:RunInProcess</c> (default <c>true</c>). Coexists with the existing
    /// <c>Microsoft.FeatureManagement</c> setup; OpenFeature is the go-forward switch for the relay. Swapping
    /// to flagd or a SaaS provider later is a provider-registration change only.
    /// </summary>
    public static IServiceCollection AddEndatixOpenFeature(this IServiceCollection services)
    {
        // AddOpenFeature registers the hosted lifecycle service itself; calling
        // AddHostedFeatureLifecycle() as well is obsolete as of OpenFeature 2.14.
        services.AddOpenFeature((OpenFeature.Hosting.OpenFeatureBuilder builder) =>
        {
            builder.AddInMemoryProvider(serviceProvider =>
            {
                var runInProcess = serviceProvider.GetService<IConfiguration>()?
                    .GetValue("Endatix:Outbox:RunInProcess", true) ?? true;

                return new Dictionary<string, Flag>
                {
                    [OutboxFlags.RelayInProcess] = new Flag<bool>(
                        new Dictionary<string, bool> { ["on"] = true, ["off"] = false },
                        runInProcess ? "on" : "off"),
                };
            });
        });

        UseSingleProcessShutdown(services);

        return services;
    }

    /// <summary>
    /// Decorates OpenFeature's <see cref="IFeatureLifecycleManager"/> with
    /// <see cref="SingleShutdownFeatureLifecycleManager"/>, so the process-wide
    /// <c>OpenFeature.Api</c> is shut down at most once no matter how many hosts stop.
    /// Idempotent: a second call finds the decorator already in place and does nothing.
    /// </summary>
    private static void UseSingleProcessShutdown(IServiceCollection services)
    {
        var descriptor = services.LastOrDefault(d => d.ServiceType == typeof(IFeatureLifecycleManager));

        // The decorator registers itself through a factory, so a null ImplementationType means
        // either that it is already installed or that OpenFeature changed how it registers the
        // manager — in both cases there is nothing safe to wrap.
        if (descriptor?.ImplementationType is null)
        {
            return;
        }

        var innerType = descriptor.ImplementationType;
        services.Remove(descriptor);
        services.Add(ServiceDescriptor.Describe(
            typeof(IFeatureLifecycleManager),
            serviceProvider => new SingleShutdownFeatureLifecycleManager(
                (IFeatureLifecycleManager)ActivatorUtilities.CreateInstance(serviceProvider, innerType)),
            descriptor.Lifetime));
    }
}
