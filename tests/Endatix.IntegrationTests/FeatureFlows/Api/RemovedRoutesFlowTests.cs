using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace Endatix.IntegrationTests;

/// <summary>
/// Routes that were removed from the API stay unmapped on the default host.
/// </summary>
[Collection(nameof(EndatixIntegrationTestCollection))]
[Trait("Category", "FeatureFlow")]
[Trait("Priority", "P1")]
public sealed class RemovedRoutesFlowTests(EndatixIntegrationWebHostFixture fixture)
{
    private const string SLACK_TOKEN_ROUTE = "slacktoken";

    [Fact]
    public void Slack_token_route_is_not_in_the_route_table()
    {
        // Arrange
        var patterns = fixture.Factory.Services
            .GetServices<EndpointDataSource>()
            .SelectMany(source => source.Endpoints)
            .OfType<RouteEndpoint>()
            .Select(endpoint => endpoint.RoutePattern.RawText ?? string.Empty)
            .ToList();

        // Assert
        Assert.Contains(patterns, pattern => pattern.Contains("auth/login", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(patterns, pattern => pattern.Contains(SLACK_TOKEN_ROUTE, StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task Post_to_the_slack_token_route_returns_not_found()
    {
        // Arrange
        var cancellationToken = TestContext.Current.CancellationToken;
        using var client = fixture.CreateClient();

        // Act
        using var response = await client.PostAsJsonAsync($"/api/{SLACK_TOKEN_ROUTE}", new { token = "unused" }, cancellationToken);

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
