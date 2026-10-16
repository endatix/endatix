extern alias EndatixWebHost;

using Endatix.IntegrationTests.Shared;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Endatix.IntegrationTests;

/// <summary>
/// A second web host on the collection's database with the Reporting module on, which the shared host leaves off.
/// The outbox relay polls every second and runs handlers in-process, so form deletion reaches Reporting on both
/// providers without the Jobs module.
/// </summary>
internal sealed class ReportingWebHost : IIntegrationTestHostFixture, IAsyncDisposable
{
    private readonly WebApplicationFactory<EndatixWebHost::Program> _factory;

    public ReportingWebHost(EndatixIntegrationWebHostFixture fixture)
    {
        Database = fixture.Database;
        _factory = new EndatixWebApplicationFactory(fixture.Database.ConnectionString, fixture.Database.Provider)
            .WithWebHostBuilder(builder =>
            {
                builder.UseSetting("Endatix:FeatureFlags:ReportingModule", "true");
                builder.UseSetting("Endatix:Outbox:DeliverToJobQueue", "false");
                builder.UseSetting("Endatix:Outbox:PollIntervalSeconds", "1");
            });
        Seed = new IntegrationSeedBuilder(_factory.Services);
    }

    public DatabaseInfrastructureFixture Database { get; }

    public DatabaseCheckpoint Checkpoint => Database.Checkpoint;

    public IServiceProvider Services => _factory.Services;

    public IntegrationSeedBuilder Seed { get; }

    public HttpClient CreateClient() => _factory.CreateClient();

    public ValueTask DisposeAsync() => _factory.DisposeAsync();
}
