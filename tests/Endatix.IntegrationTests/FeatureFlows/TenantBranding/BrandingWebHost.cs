extern alias EndatixWebHost;

using Endatix.Core.Abstractions;
using Endatix.IntegrationTests.Shared;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

namespace Endatix.IntegrationTests;

/// <summary>
/// A second web host on the collection's database with the WhiteLabeling flag on, which the shared host leaves off.
/// With <c>entitled: false</c> every tenant's plan lacks the <c>branding</c> entitlement.
/// </summary>
internal sealed class BrandingWebHost : IIntegrationTestHostFixture, IAsyncDisposable
{
    private readonly WebApplicationFactory<EndatixWebHost::Program> _factory;

    public BrandingWebHost(EndatixIntegrationWebHostFixture fixture, bool entitled = true)
    {
        Database = fixture.Database;
        _factory = new EndatixWebApplicationFactory(fixture.Database.ConnectionString, fixture.Database.Provider)
            .WithWebHostBuilder(builder =>
            {
                builder.UseSetting("Endatix:FeatureFlags:WhiteLabeling", "true");
                if (!entitled)
                {
                    builder.ConfigureTestServices(services => services.AddSingleton<ITenantEntitlements, NoEntitlements>());
                }
            });

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

    private sealed class NoEntitlements : ITenantEntitlements
    {
        public Task<bool> IsEntitledAsync(long tenantId, string name, CancellationToken cancellationToken) =>
            Task.FromResult(false);
    }
}
