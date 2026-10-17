using System.Net;
using Endatix.IntegrationTests.Shared;
using Endatix.Outbox.Engine;
using Microsoft.Extensions.DependencyInjection;

namespace Endatix.IntegrationTests;

/// <summary>
/// The shared host leaves Reporting off, and <see cref="ReportingWebHost"/> turns it on with its own outbox relay
/// closed (#1268).
/// </summary>
[Collection(nameof(EndatixIntegrationTestCollection))]
[Trait("Category", "Infrastructure")]
[Trait("Priority", "P1")]
public sealed class ReportingWebHostTests(EndatixIntegrationWebHostFixture fixture)
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
    public async Task Reporting_host_closes_its_own_outbox_relay_and_not_the_shared_one()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        bool reportingRelayAskedClosedGate;
        bool sharedOpenAlongside;

        // Act
        await using (ReportingWebHost host = new(fixture))
        {
            reportingRelayAskedClosedGate = await RelayAskedGateAsync(host, ct);
            sharedOpenAlongside = await RelayOpenAsync(fixture.Factory.Services, ct);
        }

        var sharedOpenAfter = await RelayOpenAsync(fixture.Factory.Services, ct);

        // Assert
        reportingRelayAskedClosedGate.Should().BeTrue("the running relay, not just the container, must use the closed gate");
        sharedOpenAlongside.Should().BeTrue();
        sharedOpenAfter.Should().BeTrue();
    }

    private static async Task<HttpClient> TenantAdminAsync(IIntegrationTestHostFixture host, CancellationToken ct)
    {
        var world = await host.PrepareWorldAsync(
            IntegrationWorldOptions.SingleTenant with { DefaultPassword = SeedPassword }, ct);
        return await world.AsAsync(TestPersona.TenantAdmin, cancellationToken: ct);
    }

    // Only the relay asks the closed gate, so a relay that stopped consulting it would leave the count at zero.
    private static async Task<bool> RelayAskedGateAsync(ReportingWebHost host, CancellationToken ct)
    {
        var deadline = DateTime.UtcNow + RelayTickWait;
        while (host.RelayGateChecks == 0 && DateTime.UtcNow < deadline)
        {
            await Task.Delay(200, ct);
        }

        return host.RelayGateChecks > 0;
    }

    // The relay resolves its gate from a new scope on every tick, so this is the answer its next tick gets.
    private static async Task<bool> RelayOpenAsync(IServiceProvider services, CancellationToken ct)
    {
        await using var scope = services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<IOutboxRelayGate>().IsRelayEnabledAsync(ct);
    }
}
