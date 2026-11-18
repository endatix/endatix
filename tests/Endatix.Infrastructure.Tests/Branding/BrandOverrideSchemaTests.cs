using System.Text.Json;
using Endatix.Core.Branding;
using Endatix.Infrastructure.Branding;

namespace Endatix.Infrastructure.Tests.Branding;

/// <summary>
/// A tenant brand override is the brand pack's <c>assets</c> and <c>tokens</c> groups, limited to what a tenant may set.
/// These tests tie the override reader to the embedded pack schema, so a schema change that moves those groups fails here.
/// </summary>
public sealed class BrandOverrideSchemaTests
{
    private static readonly JsonElement _schema = JsonDocument.Parse(BrandPackSchema.ReadText()).RootElement;

    [Fact]
    public void AssetKeyFilePattern_IsThePackSchemasPackFilePattern()
    {
        // Act
        var pattern = _schema.GetProperty("$defs").GetProperty("packFile").GetProperty("pattern").GetString();

        // Assert
        pattern.Should().Be(BrandOverrideReader.FILE_PATH_PATTERN);
    }

    [Fact]
    public void OverrideFields_AreFieldsOfThePackSchema()
    {
        // Arrange
        var properties = _schema.GetProperty("properties");
        var defs = _schema.GetProperty("$defs");

        // Act
        var assets = Names(properties.GetProperty("assets").GetProperty("properties"));
        var logo = Names(defs.GetProperty("themedFile").GetProperty("properties"));
        var color = Names(properties.GetProperty("tokens").GetProperty("properties").GetProperty("color").GetProperty("properties"));
        var colorToken = Names(defs.GetProperty("colorToken").GetProperty("properties"));

        // Assert
        assets.Should().Contain(["logo", "icon"]);
        logo.Should().BeEquivalentTo(["light", "dark"]);
        color.Should().BeEquivalentTo([BrandContrast.PRIMARY_FIELD, BrandContrast.PRIMARY_ON_DARK_FIELD]);
        colorToken.Should().BeEquivalentTo(["$type", "$value"]);
        _schema.GetProperty("properties").GetProperty("version").GetProperty("const").GetInt32()
            .Should().Be(BrandOverrideReader.SUPPORTED_VERSION);
    }

    private static List<string> Names(JsonElement properties) =>
        properties.EnumerateObject().Select(property => property.Name).ToList();
}
