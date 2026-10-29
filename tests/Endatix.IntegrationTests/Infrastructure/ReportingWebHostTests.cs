using System.Net;
using Endatix.IntegrationTests.Shared;
using Endatix.Outbox.Engine;
using Microsoft.Extensions.DependencyInjection;

namespace Endatix.IntegrationTests;

/// <summary>
/// The shared host leaves Reporting off, and <see cref="ReportingWebHost"/> turns it on. Its outbox relay stays off
/// whatever the shared host's does: a relay marks delivered the events it has no handler for, so two relays with
/// different modules would each run the handlers for only some events.
/// </summary>
[Collection(nameof(EndatixIntegrationTestCollection))]
[Trait("Category", "Infrastructure")]
[Trait("Priority", "P1")]
public sealed class ReportingWebHostTests(EndatixIntegrationWebHostFixture fixture) : IAsyncDisposable
{
    private const string ExportFormatsUri = "/api/settings/export-formats";
    private const string SeedPassword = "Password123!";
    private static readonly TimeSpan RelayTickWait = TimeSpan.FromSeconds(30);

    [Fact]
    public async Task Shared_host_does_not_serve_reporting_endpoints()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        using var client = await TenantAdminAsync(fixture, ct);

        // Act
        using var response = await client.GetAsync(ExportFormatsUri, ct);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Reporting_host_serves_reporting_endpoints()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        await using ReportingWebHost host = new(fixture);
        using var client = await TenantAdminAsync(host, ct);

        // Act
        using var response = await client.GetAsync(ExportFormatsUri, ct);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Starting_the_shared_hosts_relay_leaves_the_reporting_hosts_off_and_disposing_it_leaves_the_shared_one_on()
    {
        // Arrange — reset first, so the shared relay meets no message an earlier test left.
        var ct = TestContext.Current.CancellationToken;
        await fixture.PrepareWorldAsync(IntegrationWorldOptions.Empty, ct);
        fixture.BackgroundWork.StartRelay();
        bool reportingRelayAskedItsSwitch;
        bool reportingOpen;
        bool sharedOpenAlongside;

        // Act
        await using (ReportingWebHost host = new(fixture))
        {
            reportingRelayAskedItsSwitch = await RelayAskedAgainAsync(BackgroundWork.Of(host.Services), ct);
            reportingOpen = await RelayOpenAsync(host.Services, ct);
            sharedOpenAlongside = await RelayOpenAsync(fixture.Factory.Services, ct);
        }

        var sharedOpenAfterDispose = await RelayOpenAsync(fixture.Factory.Services, ct);
        await fixture.BackgroundWork.StopAsync(CancellationToken.None);
        var sharedOpenAfterStop = await RelayOpenAsync(fixture.Factory.Services, ct);

        // Assert
        reportingRelayAskedItsSwitch.Should().BeTrue("the running relay, not just the container, must ask the host's own switch");
        reportingOpen.Should().BeFalse();
        sharedOpenAlongside.Should().BeTrue();
        sharedOpenAfterDispose.Should().BeTrue("tearing the reporting host down must not switch the shared relay off");
        sharedOpenAfterStop.Should().BeFalse();
    }

    /// <summary>Stops the shared host's background work even when the test failed before it did.</summary>
    public async ValueTask DisposeAsync() => await fixture.BackgroundWork.StopAsync(CancellationToken.None);

    private static async Task<HttpClient> TenantAdminAsync(IIntegrationTestHostFixture host, CancellationToken ct)
    {
        var world = await host.PrepareWorldAsync(
            IntegrationWorldOptions.SingleTenant with { DefaultPassword = SeedPassword }, ct);
        return await world.AsAsync(TestPersona.TenantAdmin, cancellationToken: ct);
    }

    // Only the relay asks the switch on its own, so a relay that stopped consulting it would leave the count where it was.
    private static async Task<bool> RelayAskedAgainAsync(BackgroundWork background, CancellationToken ct)
    {
        var before = background.RelayChecks;
        var deadline = DateTime.UtcNow + RelayTickWait;
        while (background.RelayChecks == before && DateTime.UtcNow < deadline)
        {
            await Task.Delay(200, ct);
        }

        return background.RelayChecks > before;
    }

    // The relay resolves its gate from a new scope on every tick, so this is the answer its next tick gets.
    private static async Task<bool> RelayOpenAsync(IServiceProvider services, CancellationToken ct)
    {
        await using var scope = services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<IOutboxRelayGate>().IsRelayEnabledAsync(ct);
    }
}
