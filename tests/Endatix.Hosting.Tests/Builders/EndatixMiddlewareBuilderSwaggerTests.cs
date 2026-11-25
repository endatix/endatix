using Endatix.Api.Builders;
using Endatix.Api.Setup;
using Endatix.Hosting.Builders;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;

namespace Endatix.Hosting.Tests.Builders;

/// <summary>
/// <see cref="EndatixMiddlewareBuilder.UseSwagger"/> adds the API docs through the same once-per-application path as
/// the API middleware (endatix/endatix#1846).
/// </summary>
public sealed class EndatixMiddlewareBuilderSwaggerTests
{
    [Fact]
    public async Task UseSwagger_WithPath_MapsTheDocsPageThere()
    {
        // Arrange
        await using var app = WebApplication.CreateBuilder().Build();

        // Act
        new EndatixMiddlewareBuilder(app).UseSwagger("/docs");

        // Assert
        RoutePatterns(app).Should().Equal("/docs/{documentName?}");
    }

    [Fact]
    public async Task UseSwagger_AfterTheDocsWereAdded_MapsNothingMore()
    {
        // Arrange
        await using var app = WebApplication.CreateBuilder().Build();
        app.UseEndatixApiDocs(new ApiOptions());

        // Act
        new EndatixMiddlewareBuilder(app).UseSwagger("/docs");

        // Assert
        RoutePatterns(app).Should().Equal("/api-docs/{documentName?}");
    }

    private static IEnumerable<string?> RoutePatterns(IEndpointRouteBuilder app) =>
        app.DataSources.SelectMany(source => source.Endpoints).OfType<RouteEndpoint>()
            .Select(endpoint => endpoint.RoutePattern.RawText)
            .Where(pattern => pattern?.Contains("documentName", StringComparison.Ordinal) == true);
}
