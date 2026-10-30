using Endatix.Core.Features.Email;

namespace Endatix.Core.Tests.Features.Email;

public class EmailHelperTests
{
    [Fact]
    public void ParseJsonToDictionary_StringValue_KeepsTheText()
    {
        // Arrange
        const string json = """{"name": "Ada"}""";

        // Act
        var result = EmailHelper.ParseJsonToDictionary(json);

        // Assert
        result["name"].Should().Be("Ada");
    }

    [Fact]
    public void ParseJsonToDictionary_NullValue_MapsToEmptyString()
    {
        // Arrange
        const string json = """{"name": null}""";

        // Act
        var result = EmailHelper.ParseJsonToDictionary(json);

        // Assert
        result["name"].Should().BeEmpty();
    }

    [Fact]
    public void ParseJsonToDictionary_ArrayWithNull_JoinsTheOtherValues()
    {
        // Arrange
        const string json = """{"colors": ["red", null, "blue"]}""";

        // Act
        var result = EmailHelper.ParseJsonToDictionary(json);

        // Assert
        result["colors"].Should().Be("red, , blue");
    }

    [Fact]
    public void ParseJsonToDictionary_NestedObject_IsParsed()
    {
        // Arrange
        const string json = """{"address": {"city": "Sofia", "zip": null}}""";

        // Act
        var result = EmailHelper.ParseJsonToDictionary(json);

        // Assert
        result.Should().ContainKey("address");
    }

    [Fact]
    public void EmailWithBody_BodiesNotSet_AreEmpty()
    {
        // Act
        var email = new EmailWithBody { To = "to@example.com" };

        // Assert
        email.PlainTextBody.Should().BeEmpty();
        email.HtmlBody.Should().BeEmpty();
    }
}
