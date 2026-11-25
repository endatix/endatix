using System.Text;
using Endatix.Api.Builders;
using Endatix.Api.Setup;
using Endatix.Hosting.Builders;
using FastEndpoints;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace Endatix.Hosting.Tests.Builders;

/// <summary>
/// <see cref="EndatixMiddlewareBuilder.UseSwagger"/> adds a docs page at its path over the one document, starting from
/// the configured docs settings (endatix/endatix#1846).
/// </summary>
public sealed class EndatixMiddlewareBuilderSwaggerTests
{
    private const string API_DOCS_PATTERN = "/api-docs/{documentName?}";

    private const string SWAGGER_PATTERN = "/swagger/{documentName?}";

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
    [InlineData(null, "/swagger/{documentName?}")]
    [InlineData("/docs", "/docs/{documentName?}")]
    public async Task UseSwagger_WithConfiguredDeprecatedSwaggerPath_UsesThePathGivenElseSwagger(
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
    public async Task UseSwagger_AfterUseApi_ServesBothPages()
    {
        // Arrange
        await using var app = BuildApiApp();
        var middleware = new EndatixMiddlewareBuilder(app);
        middleware.UseApi(WithoutHostServices);

        // Act
        middleware.UseSwagger("/x");

        // Assert
        RoutePatterns(app).Should().Equal(API_DOCS_PATTERN, "/x/{documentName?}");
    }

    [Fact]
    public async Task UseSwagger_BeforeUseApi_ServesBothPages()
    {
        // Arrange
        await using var app = BuildApiApp();
        var middleware = new EndatixMiddlewareBuilder(app);

        // Act
        middleware.UseSwagger("/x");
        middleware.UseApi(WithoutHostServices);

        // Assert
        RoutePatterns(app).Should().Equal("/x/{documentName?}", API_DOCS_PATTERN);
    }

    [Fact]
    public async Task UseSwagger_WithoutAPathAfterUseApi_ServesSwaggerAndApiDocs()
    {
        // Arrange
        await using var app = BuildApiApp();
        var middleware = new EndatixMiddlewareBuilder(app);
        middleware.UseApi(WithoutHostServices);

        // Act
        middleware.UseSwagger();

        // Assert
        RoutePatterns(app).Should().Equal(API_DOCS_PATTERN, SWAGGER_PATTERN);
    }

    [Fact]
    public async Task UseSwagger_WithoutAPathBeforeUseApi_ServesSwaggerAndApiDocs()
    {
        // Arrange
        await using var app = BuildApiApp();
        var middleware = new EndatixMiddlewareBuilder(app);

        // Act
        middleware.UseSwagger();
        middleware.UseApi(WithoutHostServices);

        // Assert
        RoutePatterns(app).Should().Equal(SWAGGER_PATTERN, API_DOCS_PATTERN);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task UseSwagger_WithoutAPathAndConfiguredSwaggerUi_ServesSwaggerUiAtSwaggerAndApiDocs(bool beforeApi)
    {
        // Arrange
        await using var app = BuildApiApp(options => options.Docs.Ui = ApiDocsUi.SwaggerUi);
        var middleware = new EndatixMiddlewareBuilder(app);

        // Act
        if (beforeApi)
        {
            middleware.UseSwagger();
        }

        middleware.UseApi(WithoutHostServices);
        if (!beforeApi)
        {
            middleware.UseSwagger();
        }

        // Assert
        (await GetAsync(app, "/swagger/index.html")).Body.Should().Contain("swagger-ui");
        (await GetAsync(app, "/api-docs/index.html")).Body.Should().Contain("swagger-ui");
    }

    [Fact]
    public async Task UseSwagger_AtAPathAlreadyMapped_MapsNothingMore()
    {
        // Arrange
        await using var app = WebApplication.CreateBuilder().Build();
        app.UseEndatixApiDocs(new ApiOptions());

        // Act
        new EndatixMiddlewareBuilder(app).UseSwagger("/api-docs/");

        // Assert
        RoutePatterns(app).Should().Equal(API_DOCS_PATTERN);
    }

    [Fact]
    public async Task UseSwagger_WithSwaggerUiSettingsAndNoPath_ServesSwaggerUiAtSwagger()
    {
        // Arrange
        await using var app = WebApplication.CreateBuilder().Build();

        // Act
        new EndatixMiddlewareBuilder(app).UseSwagger(configureSwaggerUi: ui => ui.DocExpansion = "list");

        // Assert
        var (status, body) = await GetAsync(app, "/swagger/index.html");
        status.Should().Be(StatusCodes.Status200OK);
        body.Should().Contain("swagger-ui");
        RoutePatterns(app).Should().BeEmpty();
    }

    [Fact]
    public async Task UseSwagger_WithSwaggerUiSettingsAndAConfiguredUi_KeepsTheConfiguredUi()
    {
        // Arrange
        var builder = WebApplication.CreateBuilder();
        builder.Services.Configure<ApiOptions>(options => options.Docs.Ui = ApiDocsUi.Scalar);
        await using var app = builder.Build();

        // Act
        new EndatixMiddlewareBuilder(app).UseSwagger("/docs", configureSwaggerUi: ui => ui.DocExpansion = "list");

        // Assert
        RoutePatterns(app).Should().Equal("/docs/{documentName?}");
        (await GetAsync(app, "/docs/index.html")).Body.Should().NotContain("swagger-ui");
    }

    [Fact]
    public async Task UseSwagger_WithConfiguredSwaggerUiSettings_ServesSwaggerUiWithThem()
    {
        // Arrange
        var builder = WebApplication.CreateBuilder();
        builder.Services.Configure<ApiOptions>(options => options.ConfigureSwaggerUi = ui => ui.DocExpansion = "list");
        await using var app = builder.Build();

        // Act
        new EndatixMiddlewareBuilder(app).UseSwagger("/docs");

        // Assert
        RoutePatterns(app).Should().BeEmpty();
        (await GetAsync(app, "/docs/index.html")).Body.Should().Contain("swagger-ui");
    }

    private static void WithoutHostServices(ApiOptions options)
    {
        options.UseExceptionHandler = false;
        options.UseCors = false;
    }

    /// <summary>An app with what <c>UseApi</c> needs: FastEndpoints with one endpoint, and the HTTP context accessor.</summary>
    private static WebApplication BuildApiApp(Action<ApiOptions>? configure = null)
    {
        var builder = WebApplication.CreateBuilder();
        builder.Services.Configure<ApiOptions>(options => configure?.Invoke(options));
        builder.Services.AddHttpContextAccessor();
        builder.Services.AddFastEndpoints(discovery =>
        {
            discovery.DisableAutoDiscovery = true;
            discovery.Assemblies = [typeof(PingEndpoint).Assembly];
            discovery.Filter = type => type == typeof(PingEndpoint);
        });
        return builder.Build();
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

    internal sealed class PingEndpoint : EndpointWithoutRequest
    {
        public override void Configure()
        {
            Get("ping");
            AllowAnonymous();
        }

        public override Task HandleAsync(CancellationToken ct) => Send.NoContentAsync(ct);
    }
}
