using Endatix.Core.Features.WebHooks;
using Endatix.Infrastructure.Features.Outbox;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;

namespace Endatix.Infrastructure.Tests.Features.Outbox;

public class WebHookDeliveryRegistrationTests
{
    [Theory]
    [InlineData(false, typeof(WebHookOutboxIntegrationEventHandler))]
    [InlineData(true, typeof(WebHookDoorbellOutboxHandler))]
    public void AddEndatixOutboxRelay_DoorbellSetting_ResolvesExactlyOneWebHookHandler(bool enabled, Type expected)
    {
        // Arrange
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Endatix:WebHookDoorbell:Enabled"] = enabled.ToString(),
                ["Endatix:WebHookDoorbell:WorkerBaseUrl"] = "http://localhost:8081",
                ["Endatix:WebHookDoorbell:WorkerApiKey"] = "0123456789abcdef0123456789abcdef",
            })
            .Build();
        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(configuration);
        services.AddLogging();
        services.AddSingleton(Substitute.For<IWebHookService>());
        services.AddEndatixOutboxRelay();

        // Act
        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();
        var handlers = scope.ServiceProvider.GetServices<IOutboxIntegrationEventHandler>().ToList();

        // Assert
        handlers.Should().ContainSingle().Which.Should().BeOfType(expected);
    }
}
