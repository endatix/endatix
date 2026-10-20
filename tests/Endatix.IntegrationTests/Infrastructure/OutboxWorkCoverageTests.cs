using Endatix.Framework.FeatureFlags;
using Endatix.Infrastructure.Features.Outbox;
using Endatix.IntegrationTests.Infrastructure.Jobs;
using Endatix.IntegrationTests.Shared;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Endatix.IntegrationTests;

/// <summary>
/// Every module's outbox work can be delivered as background jobs: with every flag on, delivery to the job queue on
/// and the modules' inline handlers kept, the host starts only when each inline handler's events have a job
/// subscription from its own module.
/// </summary>
[Collection(nameof(DbIntegrationTestCollection))]
[Trait("Category", "Infrastructure")]
[Trait("Priority", "P1")]
public sealed class OutboxWorkCoverageTests(DbIntegrationFixture fixture)
{
    [Fact]
    public async Task Host_with_every_module_on_starts_with_job_queue_delivery()
    {
        // Arrange — the catalogue, so a module added later is covered without editing this test.
        var ct = TestContext.Current.CancellationToken;
        var everyFlagOn = FeatureFlagCatalogue.Definitions.ToDictionary(
            definition => $"Endatix:FeatureFlags:{definition.ConfigKey}", _ => (string?)"true");

        // Act — the startup check throws while any inline handler lacks its subscription.
        await using var host = await FanOutHost.StartAsync(
            fixture, new FanOutHostSetup(DeliverToJobQueue: true, _ => { }) { Settings = everyFlagOn }, ct);

        // Assert — the check ran on this host and saw the modules' handlers, so it was not vacuous.
        host.Services.GetServices<IHostedService>().Should().ContainSingle(service => service is OutboxSubscriptionsStartupCheck);
        using var scope = host.Services.CreateScope();
        var handlerAssemblies = scope.ServiceProvider.GetServices<IOutboxIntegrationEventHandler>()
            .Select(handler => handler.GetType().Assembly.GetName().Name)
            .Distinct();
        handlerAssemblies.Should().Contain(["Endatix.Modules.Reporting", "Endatix.Modules.Audience"]);
    }
}
