using Endatix.Modules.Audience.Contracts;
using Endatix.Modules.Audience.Domain;

namespace Endatix.Modules.Audience.Tests.Domain;

public class PropertyValueRulesTests
{
    private const string PlanChoices = """["basic","pro"]""";

    [Theory]
    [InlineData(AudienceDataTypeCodes.SingleChoice, null)]
    [InlineData(AudienceDataTypeCodes.MultipleChoice, "[]")]
    [InlineData(AudienceDataTypeCodes.SingleChoice, "42")]
    [InlineData(AudienceDataTypeCodes.SingleChoice, "{}")]
    [InlineData(AudienceDataTypeCodes.SingleChoice, """["a","a"]""")]
    [InlineData(AudienceDataTypeCodes.SingleChoice, """["a"," "]""")]
    public void ChoicesError_ChoiceTypeWithBadChoices_ReturnsMessage(string dataType, string? choicesJson)
    {
        // Act
        string? error = Property.ChoicesError(dataType, choicesJson, allowsOther: false);

        // Assert
        error.Should().NotBeNull();
    }

    [Theory]
    [InlineData(PlanChoices, false)]
    [InlineData(null, true)]
    public void ChoicesError_NonChoiceTypeWithChoiceSettings_ReturnsMessage(string? choicesJson, bool allowsOther)
    {
        // Act
        string? error = Property.ChoicesError(AudienceDataTypeCodes.Text, choicesJson, allowsOther);

        // Assert
        error.Should().Be("Only single choice and multiple choice properties take choices.");
    }

    [Fact]
    public void ChoicesError_ChoiceTypeWithKeys_ReturnsNull()
    {
        // Act
        string? error = Property.ChoicesError(AudienceDataTypeCodes.MultipleChoice, PlanChoices, allowsOther: true);

        // Assert
        error.Should().BeNull();
    }

    [Theory]
    [InlineData(AudienceDataTypeCodes.Number, "abc")]
    [InlineData(AudienceDataTypeCodes.Number, "1,5")]
    [InlineData(AudienceDataTypeCodes.Number, " 42 ")]
    [InlineData(AudienceDataTypeCodes.Number, "1e400")]
    [InlineData(AudienceDataTypeCodes.Boolean, "maybe")]
    [InlineData(AudienceDataTypeCodes.Boolean, "True")]
    [InlineData(AudienceDataTypeCodes.Date, "2026-13-45")]
    [InlineData(AudienceDataTypeCodes.Date, "05/10/2026")]
    [InlineData(AudienceDataTypeCodes.DateTime, "2026-10-05")]
    [InlineData(AudienceDataTypeCodes.DateTime, "tomorrow")]
    public void ValueError_ValueOfWrongType_ReturnsMessage(string dataType, string value)
    {
        // Arrange
        Property property = PropertyTests.PropertyOf(dataType);

        // Act
        string? error = property.ValueError(value);

        // Assert
        error.Should().StartWith("'Plan' ");
    }

    [Theory]
    [InlineData(AudienceDataTypeCodes.Text, "anything")]
    [InlineData(AudienceDataTypeCodes.Number, "-12.5")]
    [InlineData(AudienceDataTypeCodes.Number, "1e3")]
    [InlineData(AudienceDataTypeCodes.Number, "1e29")]
    [InlineData(AudienceDataTypeCodes.Number, "123456789012345678901234567890")]
    [InlineData(AudienceDataTypeCodes.Boolean, "false")]
    [InlineData(AudienceDataTypeCodes.Date, "2026-10-05")]
    [InlineData(AudienceDataTypeCodes.DateTime, "2026-10-05T09:30")]
    [InlineData(AudienceDataTypeCodes.DateTime, "2026-10-05T09:30:15.123Z")]
    [InlineData(AudienceDataTypeCodes.DateTime, "2026-10-05T09:30:15+03:00")]
    [InlineData(AudienceDataTypeCodes.Number, "")]
    public void ValueError_ValueOfRightType_ReturnsNull(string dataType, string value)
    {
        // Arrange
        Property property = PropertyTests.PropertyOf(dataType);

        // Act
        string? error = property.ValueError(value);

        // Assert
        error.Should().BeNull();
    }

    [Theory]
    [InlineData("pro", null)]
    [InlineData("gold", "'Plan' has no choice 'gold'.")]
    public void ValueError_SingleChoice_AcceptsKnownKeyOnly(string value, string? expected)
    {
        // Arrange
        Property property = PropertyTests.PropertyOf(AudienceDataTypeCodes.SingleChoice, PlanChoices);

        // Act
        string? error = property.ValueError(value);

        // Assert
        error.Should().Be(expected);
    }

    [Fact]
    public void ValueError_SingleChoiceAllowingOther_AcceptsUnknownKey()
    {
        // Arrange
        Property property = PropertyTests.PropertyOf(
            AudienceDataTypeCodes.SingleChoice, PlanChoices, allowsOther: true);

        // Act
        string? error = property.ValueError("gold");

        // Assert
        error.Should().BeNull();
    }

    [Theory]
    [InlineData("""["basic","pro"]""", null)]
    [InlineData("pro", "'Plan' must be a JSON array of choice keys.")]
    [InlineData("""["pro","gold"]""", "'Plan' has no choice 'gold'.")]
    public void ValueError_MultipleChoice_RequiresArrayOfKnownKeys(string value, string? expected)
    {
        // Arrange
        Property property = PropertyTests.PropertyOf(AudienceDataTypeCodes.MultipleChoice, PlanChoices);

        // Act
        string? error = property.ValueError(value);

        // Assert
        error.Should().Be(expected);
    }

    [Fact]
    public void ChoiceKeys_ChoiceProperty_ReturnsParsedKeys()
    {
        // Arrange
        Property property = PropertyTests.PropertyOf(AudienceDataTypeCodes.SingleChoice, PlanChoices);

        // Act
        IReadOnlySet<string> keys = property.ChoiceKeys();

        // Assert
        keys.Should().BeEquivalentTo(["basic", "pro"]);
        property.ChoiceKeys().Should().BeSameAs(keys);
    }
}
