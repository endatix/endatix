using Endatix.Api.Builders;
using Endatix.Api.Setup;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Endatix.Api.Tests.Setup;

/// <summary>The API docs settings in appsettings.json map to <see cref="ApiOptions"/>.</summary>
public sealed class ApiOptionsSettingsTests
{
    [Fact]
    public void Use_swagger_ui_setting_maps_to_api_options()
    {
        // Arrange
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["Endatix:Api:UseSwaggerUi"] = "true" })
            .Build();
        var services = new ServiceCollection().AddApiOptions(configuration);

        // Act
        using var provider = services.BuildServiceProvider();
        var options = provider.GetRequiredService<IOptions<ApiOptions>>().Value;

        // Assert
        options.UseSwaggerUi.Should().BeTrue();
    }

    [Fact]
    public void Swagger_ui_is_off_when_the_setting_is_absent()
    {
        // Arrange
        var services = new ServiceCollection().AddApiOptions(new ConfigurationBuilder().Build());

        // Act
        using var provider = services.BuildServiceProvider();
        var options = provider.GetRequiredService<IOptions<ApiOptions>>().Value;

        // Assert
        options.UseSwaggerUi.Should().BeFalse();
    }

    [Fact]
    public void Api_docs_path_settings_map_to_api_options()
    {
        // Arrange
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Endatix:Api:ApiDocsPath"] = "/docs",
                ["Endatix:Api:SwaggerPath"] = "/old-docs",
            })
            .Build();
        var services = new ServiceCollection().AddApiOptions(configuration);

        // Act
        using var provider = services.BuildServiceProvider();
        var options = provider.GetRequiredService<IOptions<ApiOptions>>().Value;

        // Assert
        options.ApiDocsPath.Should().Be("/docs");
#pragma warning disable CS0618 // Subject under test: the deprecated key still binds.
        options.SwaggerPath.Should().Be("/old-docs");
#pragma warning restore CS0618
    }

    [Fact]
    public void Api_docs_paths_are_unset_when_the_settings_are_absent()
    {
        // Arrange
        var services = new ServiceCollection().AddApiOptions(new ConfigurationBuilder().Build());

        // Act
        using var provider = services.BuildServiceProvider();
        var options = provider.GetRequiredService<IOptions<ApiOptions>>().Value;

        // Assert
        options.ApiDocsPath.Should().BeNull();
#pragma warning disable CS0618 // Subject under test: the deprecated key does not count as set.
        options.SwaggerPath.Should().BeNull();
#pragma warning restore CS0618
    }
}
