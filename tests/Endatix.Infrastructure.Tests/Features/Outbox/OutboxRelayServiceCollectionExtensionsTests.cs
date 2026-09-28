using Endatix.Infrastructure.Features.Outbox;
using Endatix.Outbox.Engine;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Endatix.Infrastructure.Tests.Features.Outbox;

public sealed class OutboxRelayServiceCollectionExtensionsTests
{
    [Fact]
    public void AddEndatixOutboxRelay_Default_RegistersSubscriptionCheckBeforeRelay()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        services.AddEndatixOutboxRelay();

        // Assert — hosted services start in registration order, so the check fails startup before the relay claims.
        var hostedServices = services
            .Where(descriptor => descriptor.ServiceType == typeof(IHostedService))
            .Select(descriptor => descriptor.ImplementationType)
            .ToList();
        hostedServices.Should().Contain(typeof(OutboxRelayBackgroundService));
        hostedServices.IndexOf(typeof(OutboxSubscriptionsStartupCheck))
            .Should().BeLessThan(hostedServices.IndexOf(typeof(OutboxRelayBackgroundService)));
    }
}
