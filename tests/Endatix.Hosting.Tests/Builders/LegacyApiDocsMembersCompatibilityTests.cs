using Endatix.Api.Builders;
using Endatix.Api.Configuration;
using Endatix.Hosting.Builders;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
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
    public async Task UseSwagger_WithLegacyApiOptionsMembers_BuildsAndServesSwaggerUiAtThePath()
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
        var context = new DefaultHttpContext { RequestServices = app.Services };
        context.Request.Method = HttpMethods.Get;
        context.Request.Path = "/docs/index.html";

        // Act
        new EndatixMiddlewareBuilder(app).UseSwagger(
            "/docs", settings => settings.Path = "/swagger/{documentName}/swagger.json", ui => ui.DocExpansion = "list");
        await ((IApplicationBuilder)app).Build()(context);

        // Assert
        context.Response.StatusCode.Should().Be(StatusCodes.Status200OK);
    }

    [Fact]
    public void Legacy_members_read_into_their_shipped_types()
    {
        // Arrange
        var options = new ApiOptions();
        var configuration = new ApiConfigurationOptions();

        // Act
        var useSwagger = Typed<bool>(options.UseSwagger);
        var swaggerPath = Typed<string?>(options.SwaggerPath);
        var configuredUseSwagger = Typed<bool>(configuration.UseSwagger);
        var configuredSwaggerPath = Typed<string?>(configuration.SwaggerPath);
        var ui = Typed<ApiDocsUi?>(options.Docs.Ui);
        var configuredUi = Typed<string?>(configuration.Docs.Ui);

        // Assert
        useSwagger.Should().BeTrue();
        swaggerPath.Should().Be("/api-docs");
        configuredUseSwagger.Should().BeTrue();
        configuredSwaggerPath.Should().Be("/api-docs");
        ui.Should().BeNull();
        configuredUi.Should().BeNull();
    }

    /// <summary>Reads a value as the type a host's code expects, so a changed member type fails to compile here.</summary>
    private static T Typed<T>(T value) => value;
}
