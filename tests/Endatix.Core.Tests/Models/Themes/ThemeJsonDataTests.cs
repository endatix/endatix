using Endatix.Core.Infrastructure.Result;
using Endatix.Core.Models.Themes;

namespace Endatix.Core.Tests.Models.Themes;

public class ThemeJsonDataTests
{
    [Theory]
    [InlineData("{\n  \"themeName\": \"dark\",\n  \"cssVariables\": ,\n}")]
    [InlineData("invalid-theme-data")]
    public void Create_WithUnparsableJson_ReturnsTheStaticMessage(string json)
    {
        // Act
        var result = ThemeJsonData.Create(json);

        // Assert
        result.Status.Should().Be(ResultStatus.Invalid);
        result.ValidationErrors.Should().ContainSingle()
            .Which.ErrorMessage.Should().Be("Theme JSON is invalid.");
    }

    /// <summary>
    /// <see cref="System.Text.Json.JsonException.Message"/> names .NET types and property paths, so the
    /// caller is told only that their payload did not parse.
    /// </summary>
    [Fact]
    public void Create_WithTypeMismatch_DoesNotLeakSerializerDetail()
    {
        // Act
        var result = ThemeJsonData.Create("{\"themeName\": 12345}");

        // Assert
        var message = result.ValidationErrors.Should().ContainSingle().Which.ErrorMessage;
        message.Should().Be("Theme JSON is invalid.");
        message.Should().NotContainAny("ThemeData", "System.", "Path:", "JsonException");
    }

    [Fact]
    public void Create_WithEmptyJson_ReturnsInvalid()
    {
        // Act
        var result = ThemeJsonData.Create(string.Empty);

        // Assert
        result.Status.Should().Be(ResultStatus.Invalid);
        result.ValidationErrors.Should().ContainSingle()
            .Which.ErrorMessage.Should().Be("Theme data cannot be empty");
    }
}
