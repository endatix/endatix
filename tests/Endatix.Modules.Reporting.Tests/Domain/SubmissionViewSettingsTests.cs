using Endatix.Modules.Reporting.Domain;

namespace Endatix.Modules.Reporting.Tests.Domain;

public sealed class SubmissionViewSettingsTests
{
    [Theory]
    [InlineData("""{"v":1,"columns":[]}""")]
    [InlineData("""{"v":1,"columns":[{"key":"q1"}]}""")]
    [InlineData("""{"v":1,"columns":[{"key":"q1","visible":false,"width":240},{"key":"createdAt","visible":true}],"pageSize":25,"sort":{"key":"createdAt","direction":"desc"}}""")]
    [InlineData("""{"v":1,"columns":[],"sort":null}""")]
    [InlineData("""{"v":1,"columns":[{"key":"q1","pinned":"left"}],"density":"compact"}""")]
    public void Error_WellFormedSettings_ReturnsNull(string settingsJson)
    {
        // Act
        var error = SubmissionViewSettings.Error(settingsJson);

        // Assert
        error.Should().BeNull();
    }

    [Fact]
    public void Error_DefaultSettings_ReturnsNull()
    {
        // Act
        var error = SubmissionViewSettings.Error(SubmissionViewSettings.DefaultJson);

        // Assert
        error.Should().BeNull();
    }

    [Theory]
    [InlineData(null, "Settings are required.")]
    [InlineData("  ", "Settings are required.")]
    [InlineData("{", "Settings must be valid JSON.")]
    [InlineData("[]", "Settings must be a JSON object.")]
    [InlineData("""{"columns":[]}""", "Settings must have \"v\": 1.")]
    [InlineData("""{"v":"1","columns":[]}""", "Settings must have \"v\": 1.")]
    [InlineData("""{"v":2,"columns":[]}""", "Settings must have \"v\": 1.")]
    [InlineData("""{"v":1}""", "Settings must have a \"columns\" array.")]
    [InlineData("""{"v":1,"columns":{}}""", "Settings must have a \"columns\" array.")]
    [InlineData("""{"v":1,"columns":["q1"]}""", "Each column must be a JSON object.")]
    [InlineData("""{"v":1,"columns":[{"visible":true}]}""", "Each column must have a non-empty \"key\".")]
    [InlineData("""{"v":1,"columns":[{"key":""}]}""", "Each column must have a non-empty \"key\".")]
    [InlineData("""{"v":1,"columns":[{"key":1}]}""", "Each column must have a non-empty \"key\".")]
    [InlineData("""{"v":1,"columns":[{"key":"q1"},{"key":"q1"}]}""", "Column \"q1\" is listed more than once.")]
    [InlineData("""{"v":1,"columns":[{"key":"q1","visible":"yes"}]}""", "Column \"q1\" must have a true or false \"visible\".")]
    [InlineData("""{"v":1,"columns":[{"key":"q1","width":0}]}""", "Column \"q1\" must have a \"width\" from 1 to 4000.")]
    [InlineData("""{"v":1,"columns":[{"key":"q1","width":4001}]}""", "Column \"q1\" must have a \"width\" from 1 to 4000.")]
    [InlineData("""{"v":1,"columns":[{"key":"q1","width":12.5}]}""", "Column \"q1\" must have a \"width\" from 1 to 4000.")]
    [InlineData("""{"v":1,"columns":[],"pageSize":0}""", "\"pageSize\" must be from 1 to 100.")]
    [InlineData("""{"v":1,"columns":[],"pageSize":101}""", "\"pageSize\" must be from 1 to 100.")]
    [InlineData("""{"v":1,"columns":[],"sort":"createdAt"}""", "\"sort\" must have a \"key\" and a \"direction\" of \"asc\" or \"desc\".")]
    [InlineData("""{"v":1,"columns":[],"sort":{"key":"createdAt","direction":"up"}}""", "\"sort\" must have a \"key\" and a \"direction\" of \"asc\" or \"desc\".")]
    [InlineData("""{"v":1,"columns":[],"sort":{"direction":"asc"}}""", "\"sort\" must have a \"key\" and a \"direction\" of \"asc\" or \"desc\".")]
    public void Error_MalformedSettings_ReturnsReason(string? settingsJson, string expectedError)
    {
        // Act
        var error = SubmissionViewSettings.Error(settingsJson);

        // Assert
        error.Should().Be(expectedError);
    }

    [Fact]
    public void Error_SettingsLongerThanCap_ReturnsSizeReason()
    {
        // Arrange
        var padding = new string(' ', SubmissionViewSettings.MaxJsonLength);
        var settingsJson = $$"""{"v":1,"columns":[]{{padding}}}""";

        // Act
        var error = SubmissionViewSettings.Error(settingsJson);

        // Assert
        error.Should().Be($"Settings cannot be longer than {SubmissionViewSettings.MaxJsonLength} characters.");
    }

    [Fact]
    public void Error_SettingsNestedDeeperThanAllowed_ReturnsInvalidJson()
    {
        // Arrange
        var deep = new string('[', 20) + new string(']', 20);
        var settingsJson = $$"""{"v":1,"columns":[],"extra":{{deep}}}""";

        // Act
        var error = SubmissionViewSettings.Error(settingsJson);

        // Assert
        error.Should().Be("Settings must be valid JSON.");
    }
}
