using System.Text.Json;
using System.Text.Json.Nodes;
using Endatix.Core.Helpers;

namespace Endatix.Core.Tests.Helpers;

public class JsonHelpersTests
{
    [Fact]
    public void AddOrUpdateTopLevelField_NullJson_CreatesObjectWithField()
    {
        // Act
        var result = JsonHelpers.AddOrUpdateTopLevelField(null, "key", "value");

        // Assert
        var obj = JsonNode.Parse(result) as JsonObject;
        obj.Should().NotBeNull();
        obj!["key"]!.GetValue<string>().Should().Be("value");
    }

    [Fact]
    public void AddOrUpdateTopLevelField_ExistingJson_PreservesAndUpdates()
    {
        // Arrange
        var original = "{ \"a\": 1, \"key\": \"old\" }";

        // Act
        var result = JsonHelpers.AddOrUpdateTopLevelField(original, "key", "new");

        // Assert
        var obj = JsonNode.Parse(result) as JsonObject;
        obj.Should().NotBeNull();
        obj!["a"]!.GetValue<int>().Should().Be(1);
        obj["key"]!.GetValue<string>().Should().Be("new");
    }

    [Fact]
    public void AddOrUpdateTopLevelField_InvalidJson_TreatedAsEmpty()
    {
        // Arrange
        var invalid = "not-json";

        // Act
        var result = JsonHelpers.AddOrUpdateTopLevelField(invalid, "key", "value");

        // Assert
        var obj = JsonNode.Parse(result) as JsonObject;
        obj.Should().NotBeNull();
        obj!["key"]!.GetValue<string>().Should().Be("value");
    }

    [Fact]
    public void MergeTopLevelObject_OriginalAndPatch_MergesAndOverwrites()
    {
        // Arrange
        var original = "{ \"a\": 1, \"key\": \"old\" }";
        var patch = "{ \"b\": 2, \"key\": \"new\" }";

        // Act
        var result = JsonHelpers.MergeTopLevelObject(original, patch);

        // Assert
        var obj = JsonNode.Parse(result) as JsonObject;
        obj.Should().NotBeNull();
        obj!["a"]!.GetValue<int>().Should().Be(1);
        obj["b"]!.GetValue<int>().Should().Be(2);
        obj["key"]!.GetValue<string>().Should().Be("new");
    }

    [Fact]
    public void MergeTopLevelObject_InvalidInputs_TreatedAsEmpty()
    {
        // Arrange
        var original = "not-json";
        var patch = "{ \"x\": 10 }";

        // Act
        var result = JsonHelpers.MergeTopLevelObject(original, patch);

        // Assert
        var obj = JsonNode.Parse(result) as JsonObject;
        obj.Should().NotBeNull();
        obj!["x"]!.GetValue<int>().Should().Be(10);
    }

    [Fact]
    public void MergeTopLevelObject_PatchNull_ReturnsOriginalNormalized()
    {
        // Arrange
        var original = "{ \"a\": 1 }";

        // Act
        var result = JsonHelpers.MergeTopLevelObject(original, null);

        // Assert
        var obj = JsonNode.Parse(result) as JsonObject;
        obj.Should().NotBeNull();
        obj!["a"]!.GetValue<int>().Should().Be(1);
    }
}


