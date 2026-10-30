using Endatix.Modules.Audience.Features.Imports;
using Endatix.Outbox.Engine;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;

namespace Endatix.IntegrationTests;

/// <summary>
/// What keeps a test host's background work off holds against the application's own registrations: the daily import
/// sweep is taken out, and a relay gate registered after the switch fails the host's start rather than run unswitched.
/// </summary>
[Collection(nameof(EndatixIntegrationTestCollection))]
[Trait("Category", "Infrastructure")]
[Trait("Priority", "P1")]
public sealed class TestHostSwitchedOffTests(EndatixIntegrationWebHostFixture fixture)
{
    [Fact]
    public void The_test_host_takes_out_the_one_daily_import_sweep_the_application_registers()
    {
        // Arrange
        var services = fixture.Factory.Services;

        // Act
        var removed = services.GetRequiredService<RemovedHostedServices>();
        var sweeps = services.GetServices<IHostedService>().OfType<ImportSweepService>();

        // Assert
        Assert.Equal(1, removed.ImportSweeps);
        Assert.Empty(sweeps);
    }

    [Fact]
    public async Task A_host_whose_relay_gate_is_replaced_after_the_switch_fails_to_start()
    {
        // Arrange
        await using var factory = new EndatixWebApplicationFactory(fixture.Database.ConnectionString, fixture.Database.Provider)
            .WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
                services.Replace(ServiceDescriptor.Scoped<IOutboxRelayGate, AlwaysOnOutboxRelayGate>())));

        // Act
        var startFailure = Record.Exception(() => factory.Services);

        // Assert
        Assert.NotNull(startFailure);
        Assert.Contains("not the switched one", startFailure.ToString(), StringComparison.Ordinal);
    }
}
