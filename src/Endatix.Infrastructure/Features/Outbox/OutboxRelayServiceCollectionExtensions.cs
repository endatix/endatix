using Endatix.Outbox.Engine;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using OpenFeature;
using OpenFeature.Hosting.Providers.Memory;
using OpenFeature.Providers.Memory;

namespace Endatix.Infrastructure.Features.Outbox;

/// <summary>
/// Endatix-side wiring for the <c>Endatix.Outbox.Engine</c> relay (Stage 1: in-process, webhook delivery, no
/// DAPR). Registers the engine relay loop, binds <see cref="OutboxOptions"/> from <c>Endatix:Outbox</c>, the
/// webhook publisher, and the OpenFeature gate provider. The per-provider claim store
/// (<c>AddSqlOutboxClaimStore</c>) is registered by the active persistence builder, because the dialect and
/// connection type are provider-specific.
/// </summary>
public static class OutboxRelayServiceCollectionExtensions
{
    /// <summary>
    /// Registers the in-process outbox relay: the engine loop + gate, <see cref="OutboxOptions"/> bound from
    /// the <c>Endatix:Outbox</c> config section, the <see cref="WebHookIntegrationEventPublisher"/>, and the
    /// OpenFeature provider seeding the <c>outbox-relay-in-process</c> flag.
    /// </summary>
    public static IServiceCollection AddEndatixOutboxRelay(this IServiceCollection services)
    {
        services.AddOutboxRelay();
        services.AddOptions<OutboxOptions>().BindConfiguration("Endatix:Outbox");
        services.AddScoped<IIntegrationEventPublisher, WebHookIntegrationEventPublisher>();
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
        services.AddOpenFeature((OpenFeature.Hosting.OpenFeatureBuilder builder) =>
        {
            builder.AddHostedFeatureLifecycle();
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

        return services;
    }
}
