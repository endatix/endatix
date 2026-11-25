using Endatix.Api.Builders;
using Endatix.Api.Configuration;
using Endatix.Hosting.Builders;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace Endatix.Hosting.Tests.Builders;

/// <summary>
/// The legacy docs members a host may use still compile with their shipped types. The root .editorconfig makes CS0618
/// an error in this file only, so marking one of them obsolete fails the build here, as it would for a host that treats
/// warnings as errors.
/// </summary>
public sealed class LegacyApiDocsMembersCompatibilityTests
{
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
        new EndatixMiddlewareBuilder(app).UseSwagger(
            "/docs", settings => settings.Path = "/swagger/{documentName}/swagger.json", ui => ui.DocExpansion = "list");

        // Assert
        ((IEndpointRouteBuilder)app).DataSources.SelectMany(source => source.Endpoints).OfType<RouteEndpoint>()
            .Select(endpoint => endpoint.RoutePattern.RawText)
            .Should().Contain("/docs/{documentName?}");
    }

    [Fact]
    public void Legacy_members_read_into_their_shipped_types()
    {
        // Arrange
        var options = new ApiOptions();
        var configuration = new ApiConfigurationOptions();

        // Act
        bool useSwagger = options.UseSwagger;
        string? swaggerPath = options.SwaggerPath;
        bool configuredUseSwagger = configuration.UseSwagger;
        string? configuredSwaggerPath = configuration.SwaggerPath;

        // Assert
        useSwagger.Should().BeTrue();
        swaggerPath.Should().Be("/api-docs");
        configuredUseSwagger.Should().BeTrue();
        configuredSwaggerPath.Should().Be("/api-docs");
    }
}
