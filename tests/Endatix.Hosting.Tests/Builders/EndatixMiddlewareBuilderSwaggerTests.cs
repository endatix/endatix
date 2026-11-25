using System.Text;
using Endatix.Api.Builders;
using Endatix.Api.Configuration;
using Endatix.Api.Setup;
using Endatix.Hosting.Builders;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace Endatix.Hosting.Tests.Builders;

/// <summary>
/// <see cref="EndatixMiddlewareBuilder.UseSwagger"/> adds the API docs through the same once-per-application path as
/// the API middleware, starting from the configured docs settings (endatix/endatix#1846).
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

    [Theory]
    [InlineData(null, "/configured/{documentName?}")]
    [InlineData("/docs", "/docs/{documentName?}")]
    public async Task UseSwagger_WithConfiguredDeprecatedSwaggerPath_UsesThePathGivenElseTheConfiguredOne(
        string? path, string pattern)
    {
        // Arrange
        var builder = WebApplication.CreateBuilder();
        builder.Services.Configure<ApiOptions>(options => options.SwaggerPath = "/configured");
        await using var app = builder.Build();

        // Act
        new EndatixMiddlewareBuilder(app).UseSwagger(path);

        // Assert
        RoutePatterns(app).Should().Equal(pattern);
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

    [Fact]
    public async Task UseSwagger_WithSwaggerUiSettings_ServesSwaggerUi()
    {
        // Arrange
        await using var app = WebApplication.CreateBuilder().Build();

        // Act
        new EndatixMiddlewareBuilder(app).UseSwagger(configureSwaggerUi: ui => ui.DocExpansion = "list");

        // Assert
        var (status, body) = await GetAsync(app, "/swagger/index.html");
        status.Should().Be(StatusCodes.Status200OK);
        body.Should().Contain("swagger-ui");
    }

    [Fact]
    public async Task UseSwagger_WithSwaggerUiSettingsWhileConfiguredOff_TurnsSwaggerUiOn()
    {
        // Arrange
        var builder = WebApplication.CreateBuilder();
        builder.Services.Configure<ApiOptions>(options => options.Docs.SwaggerUi.Enabled = false);
        await using var app = builder.Build();

        // Act
        new EndatixMiddlewareBuilder(app).UseSwagger(configureSwaggerUi: ui => ui.DocExpansion = "list");

        // Assert
        (await GetAsync(app, "/swagger/index.html")).Status.Should().Be(StatusCodes.Status200OK);
    }

    [Fact]
    public async Task UseSwagger_WithConfiguredSwaggerUiPath_CarriesItOver()
    {
        // Arrange
        var builder = WebApplication.CreateBuilder();
        builder.Services.Configure<ApiOptions>(options => options.Docs.SwaggerUi.Path = "/ui");
        await using var app = builder.Build();

        // Act
        new EndatixMiddlewareBuilder(app).UseSwagger("/docs");

        // Assert
        (await GetAsync(app, "/ui/index.html")).Status.Should().Be(StatusCodes.Status200OK);
        RoutePatterns(app).Should().Equal("/docs/{documentName?}");
    }

    [Fact]
    public async Task UseSwagger_AtTheSwaggerUiPath_LeavesItToSwaggerUiAndMapsTheDocsPageAtApiDocs()
    {
        // Arrange
        await using var app = WebApplication.CreateBuilder().Build();

        // Act
        new EndatixMiddlewareBuilder(app).UseSwagger("/swagger", null, ui => ui.DocExpansion = "list");

        // Assert
        RoutePatterns(app).Should().Equal("/api-docs/{documentName?}");
        (await GetAsync(app, "/swagger/index.html")).Body.Should().Contain("swagger-ui");
    }

    /// <summary>
    /// The legacy members still compile without a warning, so a host that treats warnings as errors builds unchanged.
    /// Marking one of them obsolete again shows here as a CS0618 warning.
    /// </summary>
    [Fact]
    public async Task UseSwagger_WithLegacyApiOptionsMembers_BuildsAndMapsTheDocsPage()
    {
        // Arrange
        var builder = WebApplication.CreateBuilder();
        builder.Services.Configure<ApiOptions>(options =>
        {
            options.UseSwagger = true;
            options.SwaggerPath = "/legacy";
            options.ConfigureSwaggerUi = ui => ui.DocExpansion = "list";
        });
        await using var app = builder.Build();

        // Act
        new EndatixMiddlewareBuilder(app).UseSwagger(null, settings => settings.Path = "/swagger/{documentName}/swagger.json", null);

        bool configuredUseSwagger = new ApiConfigurationOptions().UseSwagger;

        // Assert
        RoutePatterns(app).Should().Equal("/legacy/{documentName?}");
        configuredUseSwagger.Should().BeTrue();
    }

    /// <summary>Runs one GET through the app's middleware. Swagger UI is middleware, not an endpoint.</summary>
    private static async Task<(int Status, string Body)> GetAsync(IApplicationBuilder app, string path)
    {
        var pipeline = app.Build();
        var context = new DefaultHttpContext { RequestServices = app.ApplicationServices };
        context.Request.Method = HttpMethods.Get;
        context.Request.Path = path;
        using var body = new MemoryStream();
        context.Response.Body = body;

        await pipeline(context);
        return (context.Response.StatusCode, Encoding.UTF8.GetString(body.ToArray()));
    }

    private static IEnumerable<string?> RoutePatterns(IEndpointRouteBuilder app) =>
        app.DataSources.SelectMany(source => source.Endpoints).OfType<RouteEndpoint>()
            .Select(endpoint => endpoint.RoutePattern.RawText)
            .Where(pattern => pattern?.Contains("documentName", StringComparison.Ordinal) == true);
}
