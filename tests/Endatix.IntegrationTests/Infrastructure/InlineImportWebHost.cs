extern alias EndatixWebHost;

using Endatix.IntegrationTests.Shared;
using Endatix.Modules.Audience.Features.Imports;
using Endatix.Outbox.Engine;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Endatix.IntegrationTests;

/// <summary>
/// A second web host on the collection's database that runs staged imports in the request, as a host without the
/// job queue does. The shared host runs them as background jobs. A test opts into it per test.
/// </summary>
/// <remarks>
/// Only the import runner changes: the job queue itself stays on, since <c>Endatix:BackgroundJobs</c> settings are read
/// process-wide. Its outbox relay stays closed, so the shared host alone delivers the events both hosts raise.
/// </remarks>
internal sealed class InlineImportWebHost : IIntegrationTestHostFixture, IAsyncDisposable
{
    private readonly WebApplicationFactory<EndatixWebHost::Program> _factory;

    public InlineImportWebHost(EndatixIntegrationWebHostFixture fixture)
    {
        Database = fixture.Database;
        _factory = new EndatixWebApplicationFactory(fixture.Database.ConnectionString, fixture.Database.Provider)
            .WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
            {
                services.Replace(ServiceDescriptor.Singleton<IOutboxRelayGate>(new ClosedOutboxRelayGate()));
                services.Replace(ServiceDescriptor.Scoped<IImportRunner>(provider =>
                    new InlineImportRunner(provider.GetRequiredService<ImportRun>())));
            }));

        // Reading Services starts the host; a failed start would otherwise leave it running.
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

    private sealed class ClosedOutboxRelayGate : IOutboxRelayGate
    {
        public Task<bool> IsRelayEnabledAsync(CancellationToken cancellationToken) => Task.FromResult(false);
    }
}
