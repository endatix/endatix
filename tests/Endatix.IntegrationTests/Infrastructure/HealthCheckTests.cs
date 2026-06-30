using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

namespace Endatix.IntegrationTests;

[Collection(nameof(EndatixIntegrationTestCollection))]
[Trait("Category", "Infrastructure")]
[Trait("Priority", "P0")]
public sealed class HealthCheckTests
{
    private readonly EndatixIntegrationWebHostFixture _fixture;

    public HealthCheckTests(EndatixIntegrationWebHostFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task Health_endpoint_returns_success()
    {
        // Arrange
        var cancellationToken = TestContext.Current.CancellationToken;
        var client = _fixture.Factory.CreateClient();

        // Act
        var response = await client.GetAsync(new Uri("/health", UriKind.Relative), cancellationToken);

        // Assert
        response.EnsureSuccessStatusCode();
    }

    [Fact]
    public async Task Health_endpoint_returns_success_with_per_test_service_override()
    {
        // Arrange
        var cancellationToken = TestContext.Current.CancellationToken;
        await using EndatixWebApplicationFactory baseFactory = new(_fixture.Database.ConnectionString, _fixture.Database.Provider);
        var factory = baseFactory
            .WithWebHostBuilder(builder =>
            {
                builder.ConfigureTestServices(services =>
                {
                    services.AddSingleton<IntegrationTestMarkerService>();
                });
            });
        var markerService = factory.Services.GetRequiredService<IntegrationTestMarkerService>();
        Assert.NotNull(markerService);
        var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });

        // Act
        var response = await client.GetAsync(new Uri("/health", UriKind.Relative), cancellationToken);

        // Assert
        response.EnsureSuccessStatusCode();
    }
}

internal sealed class IntegrationTestMarkerService;
