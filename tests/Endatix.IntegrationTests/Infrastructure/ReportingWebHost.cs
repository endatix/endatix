extern alias EndatixWebHost;

using Endatix.IntegrationTests.Shared;
using Endatix.Outbox.Engine;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Endatix.IntegrationTests;

/// <summary>
/// A second web host on the collection's database with the Reporting module on, which the shared host leaves off.
/// A test class that calls Reporting endpoints opts into it per test.
/// </summary>
/// <remarks>
/// Its outbox relay stays closed. The shared host polls the same outbox with Reporting off, and a relay with no handler
/// for an event marks it delivered, so a Reporting handler here would run for some events and not others. Tests
/// trigger Reporting work directly instead (compile, backfill). The gate is per host: <c>Endatix:Outbox:RunInProcess</c>
/// feeds the process-wide OpenFeature API, so it would close the shared host's relay too.
/// </remarks>
internal sealed class ReportingWebHost : IIntegrationTestHostFixture, IAsyncDisposable
{
    private readonly WebApplicationFactory<EndatixWebHost::Program> _factory;
    private readonly ClosedOutboxRelayGate _relayGate = new();

    public ReportingWebHost(EndatixIntegrationWebHostFixture fixture)
    {
        Database = fixture.Database;
        _factory = new EndatixWebApplicationFactory(fixture.Database.ConnectionString, fixture.Database.Provider)
            .WithWebHostBuilder(builder =>
            {
                builder.UseSetting("Endatix:FeatureFlags:ReportingModule", "true");
                builder.ConfigureTestServices(services =>
                    services.Replace(ServiceDescriptor.Singleton<IOutboxRelayGate>(_relayGate)));
            });

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

    /// <summary>How many times this host's outbox relay has asked its gate whether to run.</summary>
    public int RelayGateChecks => _relayGate.Checks;

    public HttpClient CreateClient() => _factory.CreateClient();

    public ValueTask DisposeAsync() => _factory.DisposeAsync();

    private sealed class ClosedOutboxRelayGate : IOutboxRelayGate
    {
        private int _checks;

        public int Checks => Volatile.Read(ref _checks);

        public Task<bool> IsRelayEnabledAsync(CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref _checks);
            return Task.FromResult(false);
        }
    }
}
