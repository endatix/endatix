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
        bool reportingOpen;
        bool sharedOpenAlongside;

        // Act
        await using (ReportingWebHost host = new(fixture))
        {
            reportingOpen = await RelayOpenAsync(host.Services, ct);
            sharedOpenAlongside = await RelayOpenAsync(fixture.Factory.Services, ct);
        }

        var sharedOpenAfter = await RelayOpenAsync(fixture.Factory.Services, ct);

        // Assert
        reportingOpen.Should().BeFalse();
        sharedOpenAlongside.Should().BeTrue();
        sharedOpenAfter.Should().BeTrue();
    }

    private static async Task<HttpClient> TenantAdminAsync(IIntegrationTestHostFixture host, CancellationToken ct)
    {
        var world = await host.PrepareWorldAsync(
            IntegrationWorldOptions.SingleTenant with { DefaultPassword = "Password123!" }, ct);
        return await world.AsAsync(TestPersona.TenantAdmin, cancellationToken: ct);
    }

    // The relay resolves its gate from a new scope on every tick, so this is the answer its next tick gets.
    private static async Task<bool> RelayOpenAsync(IServiceProvider services, CancellationToken ct)
    {
        await using var scope = services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<IOutboxRelayGate>().IsRelayEnabledAsync(ct);
    }
}
