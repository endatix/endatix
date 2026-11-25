using Endatix.Api.Builders;
using Endatix.Api.Configuration;
using Endatix.Api.Setup;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Endatix.Api.Tests.Setup;

/// <summary>
/// The API docs settings in appsettings.json map to <see cref="ApiOptions.Docs"/>: the nested <c>Docs</c> keys win, and
/// the legacy <c>UseSwagger</c> and <c>SwaggerPath</c> keys still apply when the new ones are unset.
/// </summary>
public sealed class ApiOptionsSettingsTests
{
    [Fact]
    public void Docs_settings_are_the_defaults_when_no_key_is_set()
    {
        // Arrange
        var configuration = Configuration([]);

        // Act
        var docs = Resolve(configuration).Docs;

        // Assert
        docs.Enabled.Should().BeTrue();
        docs.Path.Should().BeNull();
        docs.SwaggerUi.Enabled.Should().BeNull();
        docs.SwaggerUi.Path.Should().BeNull();
        docs.Conflicts.Should().BeEmpty();
    }

    [Fact]
    public void Fresh_options_read_the_shipped_swagger_path_default()
    {
        // Arrange
        var options = new ApiOptions();
        var configuration = new ApiConfigurationOptions();

        // Act
        string? optionsPath = options.SwaggerPath;
        string? configurationPath = configuration.SwaggerPath;

        // Assert
        optionsPath.Should().Be("/api-docs");
        configurationPath.Should().Be("/api-docs");
        options.Docs.Path.Should().BeNull();
    }

    [Fact]
    public void Nested_docs_settings_map_to_api_options()
    {
        // Arrange
        var configuration = Configuration(new()
        {
            ["Endatix:Api:Docs:Enabled"] = "false",
            ["Endatix:Api:Docs:Path"] = "/docs",
            ["Endatix:Api:Docs:SwaggerUi:Enabled"] = "true",
            ["Endatix:Api:Docs:SwaggerUi:Path"] = "/ui",
        });

        // Act
        var docs = Resolve(configuration).Docs;

        // Assert
        docs.Enabled.Should().BeFalse();
        docs.Path.Should().Be("/docs");
        docs.SwaggerUi.Enabled.Should().BeTrue();
        docs.SwaggerUi.Path.Should().Be("/ui");
    }

    [Fact]
    public void Legacy_keys_apply_when_the_nested_ones_are_unset()
    {
        // Arrange
        var configuration = Configuration(new()
        {
            ["Endatix:Api:UseSwagger"] = "false",
            ["Endatix:Api:SwaggerPath"] = "/old-docs",
        });

        // Act
        var options = Resolve(configuration);

        // Assert
        options.Docs.Enabled.Should().BeFalse();
        options.Docs.Path.Should().Be("/old-docs");
        options.UseSwagger.Should().BeFalse();
        options.SwaggerPath.Should().Be("/old-docs");
        options.Docs.PathSetting.Should().Be(ApiDocsOptions.SWAGGER_PATH_SETTING);
        options.Docs.Conflicts.Should().BeEmpty();
    }

    [Fact]
    public void Nested_keys_win_over_different_legacy_keys_and_record_each_conflict()
    {
        // Arrange
        var configuration = Configuration(new()
        {
            ["Endatix:Api:Docs:Enabled"] = "false",
            ["Endatix:Api:UseSwagger"] = "true",
            ["Endatix:Api:Docs:Path"] = "/new",
            ["Endatix:Api:SwaggerPath"] = "/old",
        });

        // Act
        var docs = Resolve(configuration).Docs;

        // Assert
        docs.Enabled.Should().BeFalse();
        docs.Path.Should().Be("/new");
        docs.PathSetting.Should().Be(ApiDocsOptions.PATH_SETTING);
        docs.Conflicts.Select(conflict => (conflict.Setting, conflict.Value, conflict.LegacySetting, conflict.LegacyValue))
            .Should().Equal(
                ("Endatix:Api:Docs:Enabled", "False", "Endatix:Api:UseSwagger", "True"),
                ("Endatix:Api:Docs:Path", "/new", "Endatix:Api:SwaggerPath", "/old"));
    }

    [Fact]
    public void Docs_enabled_false_without_use_swagger_records_no_conflict()
    {
        // Arrange
        var configuration = Configuration(new() { ["Endatix:Api:Docs:Enabled"] = "false" });

        // Act
        var docs = Resolve(configuration).Docs;

        // Assert
        docs.Enabled.Should().BeFalse();
        docs.Conflicts.Should().BeEmpty();
    }

    [Fact]
    public void Equal_nested_and_legacy_keys_record_no_conflict()
    {
        // Arrange
        var configuration = Configuration(new()
        {
            ["Endatix:Api:Docs:Enabled"] = "true",
            ["Endatix:Api:UseSwagger"] = "true",
            ["Endatix:Api:Docs:Path"] = "/docs",
            ["Endatix:Api:SwaggerPath"] = "/docs",
        });

        // Act
        var docs = Resolve(configuration).Docs;

        // Assert
        docs.Conflicts.Should().BeEmpty();
    }

    [Fact]
    public void Legacy_properties_forward_to_the_docs_settings()
    {
        // Arrange
        var options = new ApiOptions();

        // Act
        options.UseSwagger = false;
        options.SwaggerPath = "/old";

        // Assert
        options.Docs.Enabled.Should().BeFalse();
        options.Docs.Path.Should().Be("/old");
        options.Docs.PathSetting.Should().Be(ApiDocsOptions.SWAGGER_PATH_SETTING);
    }

    [Fact]
    public void Setting_the_docs_path_after_the_legacy_path_names_the_docs_path_setting()
    {
        // Arrange
        var options = new ApiOptions { SwaggerPath = "/old" };

        // Act
        options.Docs.Path = "/new";

        // Assert
        options.SwaggerPath.Should().Be("/new");
        options.Docs.PathSetting.Should().Be(ApiDocsOptions.PATH_SETTING);
    }

    [Fact]
    public void Explicit_docs_copy_without_a_path_keeps_the_path_its_setting_and_its_conflict()
    {
        // Arrange
        var docs = Resolve(Configuration(new()
        {
            ["Endatix:Api:Docs:Enabled"] = "false",
            ["Endatix:Api:UseSwagger"] = "true",
            ["Endatix:Api:SwaggerPath"] = "",
        })).Docs;

        // Act
        var copy = docs.ForExplicitDocs(path: null, turnSwaggerUiOn: true);

        // Assert
        copy.Should().NotBeSameAs(docs);
        copy.Enabled.Should().BeTrue();
        copy.Path.Should().BeEmpty();
        copy.PathSetting.Should().Be(ApiDocsOptions.SWAGGER_PATH_SETTING);
        copy.SwaggerUi.Enabled.Should().BeTrue();
        copy.Conflicts.Should().BeEmpty();
    }

    [Fact]
    public void Explicit_docs_copy_with_a_path_drops_the_path_conflict()
    {
        // Arrange
        var docs = Resolve(Configuration(new()
        {
            ["Endatix:Api:Docs:Path"] = "/new",
            ["Endatix:Api:SwaggerPath"] = "/old",
            ["Endatix:Api:Docs:SwaggerUi:Enabled"] = "false",
        })).Docs;

        // Act
        var withoutPath = docs.ForExplicitDocs(path: null, turnSwaggerUiOn: false);
        var withPath = docs.ForExplicitDocs("/given", turnSwaggerUiOn: false);

        // Assert
        withoutPath.Conflicts.Should().ContainSingle(conflict => conflict.Setting == ApiDocsOptions.PATH_SETTING);
        withPath.Conflicts.Should().BeEmpty();
        withPath.Path.Should().Be("/given");
        withPath.SwaggerUi.Enabled.Should().BeFalse();
    }

    private static IConfiguration Configuration(Dictionary<string, string?> values) =>
        new ConfigurationBuilder().AddInMemoryCollection(values).Build();

    private static ApiOptions Resolve(IConfiguration configuration)
    {
        using var provider = new ServiceCollection().AddApiOptions(configuration).BuildServiceProvider();
        return provider.GetRequiredService<IOptions<ApiOptions>>().Value;
    }
}
