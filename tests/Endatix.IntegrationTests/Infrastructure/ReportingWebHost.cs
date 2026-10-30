extern alias EndatixWebHost;

using Endatix.IntegrationTests.Shared;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Endatix.IntegrationTests;

/// <summary>
/// A second web host on the collection's database with the Reporting module on, which the shared host leaves off.
/// A test class that calls Reporting endpoints opts into it per test.
/// </summary>
/// <remarks>
/// Tests leave its <see cref="BackgroundWork"/> off. A relay with no handler for an event marks it delivered, so with
/// the shared host's relay on, a Reporting handler here would run for some events and not others. Tests trigger
/// Reporting work directly instead (compile, backfill).
/// </remarks>
internal sealed class ReportingWebHost : IIntegrationTestHostFixture, IAsyncDisposable
{
    private readonly WebApplicationFactory<EndatixWebHost::Program> _factory;

    public ReportingWebHost(EndatixIntegrationWebHostFixture fixture)
    {
        Database = fixture.Database;
        _factory = new EndatixWebApplicationFactory(fixture.Database.ConnectionString, fixture.Database.Provider)
            .WithWebHostBuilder(builder => builder.UseSetting("Endatix:FeatureFlags:ReportingModule", "true"));

        // Reading Services starts the host; a failed start would otherwise leave it running, since the caller never
        // gets an instance to dispose.
        try
        {
            Seed = new IntegrationSeedBuilder(_factory.Services);
        }
        catch
        {
            _factory.Dispose();
            throw;
        }
    }

    public DatabaseInfrastructureFixture Database { get; }

    public DatabaseCheckpoint Checkpoint => Database.Checkpoint;

    public IServiceProvider Services => _factory.Services;

    public IntegrationSeedBuilder Seed { get; }

    public HttpClient CreateClient() => _factory.CreateClient();

    public ValueTask DisposeAsync() => _factory.DisposeAsync();
}
