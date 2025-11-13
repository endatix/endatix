using Endatix.Core.Infrastructure.Result;
using Endatix.Infrastructure.Utils;

namespace Endatix.Infrastructure.Tests.Utils;

public sealed class JsonExtractorTests
{
    [Fact]
    public void Constructor_NullContent_ThrowsArgumentNullException()
    {
        // Arrange & Act
        var action = () => new JsonExtractor(null!);

        // Assert
        action.Should().Throw<ArgumentNullException>();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("\t")]
    [InlineData("\n")]
    public void Constructor_WhitespaceContent_ThrowsArgumentException(string content)
    {
        // Arrange & Act
        var action = () => new JsonExtractor(content);

        // Assert
        action.Should().Throw<ArgumentException>();
    }

    [Theory]
    [InlineData("invalid json")]
    [InlineData("{ invalid }")]
    [InlineData("[")]
    [InlineData("{ \"key\": }")]
    public void Constructor_InvalidJson_ThrowsInvalidOperationException(string invalidJson)
    {
        // Arrange & Act
        var action = () => new JsonExtractor(invalidJson);

        // Assert
        action.Should().Throw<InvalidOperationException>()
            .WithMessage("Failed to parse JSON*");
    }

    [Fact]
    public void Constructor_ValidJson_SuccessfullyParses()
    {
        // Arrange
        var validJson = """{ "key": "value" }""";

        // Act
        using var extractor = new JsonExtractor(validJson);

        // Assert
        extractor.Should().NotBeNull();
        extractor.RootElement.ValueKind.Should().Be(System.Text.Json.JsonValueKind.Object);
    }

    [Fact]
    public void RootElement_ValidJson_ReturnsRootElement()
    {
        // Arrange
        var json = """{ "name": "test" }""";
        using var extractor = new JsonExtractor(json);

        // Act
        var rootElement = extractor.RootElement;

        // Assert
        rootElement.ValueKind.Should().Be(System.Text.Json.JsonValueKind.Object);
        rootElement.TryGetProperty("name", out var nameProperty).Should().BeTrue();
        nameProperty.GetString().Should().Be("test");
    }

    [Fact]
    public void ExtractArrayOfStrings_NullPath_ReturnsInvalid()
    {
        // Arrange
        var json = """{ "roles": ["admin", "user"] }""";
        using var extractor = new JsonExtractor(json);

        // Act
        var result = extractor.ExtractArrayOfStrings(null!);

        // Assert
        result.IsInvalid().Should().BeTrue();
        result.ValidationErrors.Should().Contain(e => e.ErrorMessage.Contains("Path is required"));
    }

    [Fact]
    public void ExtractArrayOfStrings_EmptyPath_ReturnsInvalid()
    {
        // Arrange
        var json = """{ "roles": ["admin", "user"] }""";
        using var extractor = new JsonExtractor(json);

        // Act
        var result = extractor.ExtractArrayOfStrings(string.Empty);

        // Assert
        result.IsInvalid().Should().BeTrue();
        result.ValidationErrors.Should().Contain(e => e.ErrorMessage.Contains("Path is required"));
    }

    [Fact]
    public void ExtractArrayOfStrings_PathDoesNotExist_ReturnsNotFound()
    {
        // Arrange
        var json = """{ "other": "value" }""";
        using var extractor = new JsonExtractor(json);

        // Act
        var result = extractor.ExtractArrayOfStrings("roles");

        // Assert
        result.Status.Should().Be(ResultStatus.NotFound);
    }

    [Fact]
    public void ExtractArrayOfStrings_NestedPathDoesNotExist_ReturnsNotFound()
    {
        // Arrange
        var json = """{ "resource_access": { "other-app": { "roles": ["admin"] } } }""";
        using var extractor = new JsonExtractor(json);

        // Act
        var result = extractor.ExtractArrayOfStrings("resource_access.endatix-hub.roles");

        // Assert
        result.Status.Should().Be(ResultStatus.NotFound);
    }

    [Fact]
    public void ExtractArrayOfStrings_ElementIsNotArray_ReturnsInvalid()
    {
        // Arrange
        var json = """{ "roles": "not-an-array" }""";
        using var extractor = new JsonExtractor(json);

        // Act
        var result = extractor.ExtractArrayOfStrings("roles");

        // Assert
        result.IsInvalid().Should().BeTrue();
        result.ValidationErrors.Should().Contain(e => e.ErrorMessage.Contains("not JSON array"));
    }

    [Fact]
    public void ExtractArrayOfStrings_ElementIsObject_ReturnsInvalid()
    {
        // Arrange
        var json = """{ "roles": { "admin": true } }""";
        using var extractor = new JsonExtractor(json);

        // Act
        var result = extractor.ExtractArrayOfStrings("roles");

        // Assert
        result.IsInvalid().Should().BeTrue();
        result.ValidationErrors.Should().Contain(e => e.ErrorMessage.Contains("not JSON array"));
    }

    [Fact]
    public void ExtractArrayOfStrings_SimplePath_ReturnsArray()
    {
        // Arrange
        var json = """{ "roles": ["admin", "user", "guest"] }""";
        using var extractor = new JsonExtractor(json);

        // Act
        var result = extractor.ExtractArrayOfStrings("roles");

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeEquivalentTo(new[] { "admin", "user", "guest" });
    }

    [Fact]
    public void ExtractArrayOfStrings_NestedPath_ReturnsArray()
    {
        // Arrange
        var json = """{ "resource_access": { "endatix-hub": { "roles": ["admin", "creator"] } } }""";
        using var extractor = new JsonExtractor(json);

        // Act
        var result = extractor.ExtractArrayOfStrings("resource_access.endatix-hub.roles");

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeEquivalentTo(new[] { "admin", "creator" });
    }

    [Fact]
    public void ExtractArrayOfStrings_EmptyArray_ReturnsEmptyArray()
    {
        // Arrange
        var json = """{ "roles": [] }""";
        using var extractor = new JsonExtractor(json);

        // Act
        var result = extractor.ExtractArrayOfStrings("roles");

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeEmpty();
    }

    [Fact]
    public void ExtractArrayOfStrings_ArrayWithNullValues_ConvertsNullsToEmptyStrings()
    {
        // Arrange
        var json = """{ "roles": ["admin", null, "user", null] }""";
        using var extractor = new JsonExtractor(json);

        // Act
        var result = extractor.ExtractArrayOfStrings("roles");

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeEquivalentTo(new[] { "admin", string.Empty, "user", string.Empty });
    }

    [Fact]
    public void ExtractArrayOfStrings_ArrayWithMixedTypes_HandlesNonStringValues()
    {
        // Arrange
        var json = """{ "roles": ["admin", 123, "user", true] }""";
        using var extractor = new JsonExtractor(json);

        // Act
        var result = extractor.ExtractArrayOfStrings("roles");

       
        // Assert
        result.Status.Should().Be(ResultStatus.Ok);
        result.Value.Should().HaveCount(4);
        result.Value[0].Should().Be("admin");
        result.Value[1].Should().Be(string.Empty); // Non-string values become empty string
        result.Value[2].Should().Be("user");
        result.Value[3].Should().Be(string.Empty); // Non-string values become empty string
    }

    [Fact]
    public void ExtractArrayOfStrings_DeeplyNestedPath_ReturnsArray()
    {
        // Arrange
        var json = """{ "level1": { "level2": { "level3": { "level4": { "roles": ["deep-admin"] } } } } }""";
        using var extractor = new JsonExtractor(json);

        // Act
        var result = extractor.ExtractArrayOfStrings("level1.level2.level3.level4.roles");

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeEquivalentTo(new[] { "deep-admin" });
    }

    [Fact]
    public void Dispose_CalledOnce_DisposesDocument()
    {
        // Arrange
        var json = """{ "test": "value" }""";
        var extractor = new JsonExtractor(json);

        // Act
        extractor.Dispose();

        // Assert
        // Should not throw on dispose
        extractor.Dispose();
    }

    [Fact]
    public void Dispose_CalledMultipleTimes_DoesNotThrow()
    {
        // Arrange
        var json = """{ "test": "value" }""";
        var extractor = new JsonExtractor(json);

        // Act & Assert
        extractor.Dispose();
        extractor.Dispose();
        extractor.Dispose();
        // Should not throw
    }

    [Fact]
    public void ExtractArrayOfStrings_RealWorldKeycloakResponse_ExtractsRolesCorrectly()
    {
        // Arrange - Simulating a real Keycloak introspection response
        var json = """
        {
            "active": true,
            "exp": 1234567890,
            "resource_access": {
                "endatix-hub": {
                    "roles": ["admin", "platform-admin", "creator"]
                }
            }
        }
        """;
        using var extractor = new JsonExtractor(json);

        // Act
        var result = extractor.ExtractArrayOfStrings("resource_access.endatix-hub.roles");

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeEquivalentTo(new[] { "admin", "platform-admin", "creator" });
    }

    [Fact]
    public void ExtractArrayOfStrings_ArrayWithEmptyStrings_PreservesEmptyStrings()
    {
        // Arrange
        var json = """{ "roles": ["admin", "", "user", ""] }""";
        using var extractor = new JsonExtractor(json);

        // Act
        var result = extractor.ExtractArrayOfStrings("roles");

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeEquivalentTo(new[] { "admin", string.Empty, "user", string.Empty });
    }
}

