using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

namespace Endatix.IntegrationTests;

[Collection(nameof(OssIntegrationTestCollection))]
[Trait("Category", "Infrastructure")]
[Trait("Priority", "P0")]
public sealed class HealthCheckTests
{
    private readonly OssIntegrationWebHostFixture _fixture;

    public HealthCheckTests(OssIntegrationWebHostFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task Health_endpoint_returns_success()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var client = _fixture.Factory.CreateClient();
        var response = await client.GetAsync(new Uri("/health", UriKind.Relative), cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    [Fact]
    public async Task Health_endpoint_returns_success_with_per_test_service_override()
    {
        // Arrange
        var cancellationToken = TestContext.Current.CancellationToken;
        await using OssWebApplicationFactory baseFactory = new(_fixture.Database.ConnectionString, _fixture.Database.Provider);
        var client = baseFactory
            .WithWebHostBuilder(builder =>
            {
                builder.ConfigureTestServices(services =>
                {
                    services.AddSingleton<IntegrationTestMarkerService>();
                });
            })
            .CreateClient(new WebApplicationFactoryClientOptions
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
