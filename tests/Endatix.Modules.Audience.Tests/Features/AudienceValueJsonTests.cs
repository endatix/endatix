using System.Text.Json.Nodes;
using Endatix.Modules.Audience.Contracts;
using Endatix.Modules.Audience.Domain;
using Endatix.Modules.Audience.Features;
using Endatix.Modules.Audience.Tests.Domain;

namespace Endatix.Modules.Audience.Tests.Features;

public sealed class AudienceValueJsonTests
{
    [Theory]
    [InlineData(AudienceDataTypeCodes.Text, "Columbus", "\"Columbus\"")]
    [InlineData(AudienceDataTypeCodes.Text, "42", "\"42\"")]
    [InlineData(AudienceDataTypeCodes.Number, "12", "12")]
    [InlineData(AudienceDataTypeCodes.Number, "-0.5", "-0.5")]
    [InlineData(AudienceDataTypeCodes.Number, "NaN", "\"NaN\"")]
    [InlineData(AudienceDataTypeCodes.Number, "-Infinity", "\"-Infinity\"")]
    [InlineData(AudienceDataTypeCodes.Boolean, "true", "true")]
    [InlineData(AudienceDataTypeCodes.Boolean, "false", "false")]
    [InlineData(AudienceDataTypeCodes.Date, "2026-10-21", "\"2026-10-21\"")]
    [InlineData(AudienceDataTypeCodes.DateTime, "2026-10-21T09:30:00+03:00", "\"2026-10-21T09:30:00+03:00\"")]
    [InlineData(AudienceDataTypeCodes.SingleChoice, "gold", "\"gold\"")]
    [InlineData(AudienceDataTypeCodes.MultipleChoice, """["a","b"]""", """["a","b"]""")]
    public void Typed_StoredValue_IsTheJsonTheRespondentReads(string dataType, string value, string expectedJson)
    {
        // Act
        JsonNode? typed = AudienceValueJson.Typed(dataType, value);

        // Assert
        JsonNode.DeepEquals(typed, JsonNode.Parse(expectedJson)).Should().BeTrue();
    }

    [Fact]
    public void Typed_EmptyValue_IsNull()
    {
        AudienceValueJson.Typed(AudienceDataTypeCodes.Number, "").Should().BeNull();
    }

    [Fact]
    public void Typed_DateTime_KeepsItsOffset()
    {
        // Act
        JsonNode? typed = AudienceValueJson.Typed(AudienceDataTypeCodes.DateTime, "2026-10-21T23:30-05:00");

        // Assert
        typed!.GetValue<string>().Should().Be("2026-10-21T23:30-05:00");
    }

    [Theory]
    [InlineData(AudienceDataTypeCodes.Text, "Columbus")]
    [InlineData(AudienceDataTypeCodes.Text, "42")]
    [InlineData(AudienceDataTypeCodes.Number, "12")]
    [InlineData(AudienceDataTypeCodes.Number, "-0.5")]
    [InlineData(AudienceDataTypeCodes.Number, "12.5")]
    [InlineData(AudienceDataTypeCodes.Boolean, "true")]
    [InlineData(AudienceDataTypeCodes.Boolean, "false")]
    [InlineData(AudienceDataTypeCodes.Date, "2026-10-21")]
    [InlineData(AudienceDataTypeCodes.DateTime, "2026-10-21T09:30:00+03:00")]
    [InlineData(AudienceDataTypeCodes.SingleChoice, "gold")]
    [InlineData(AudienceDataTypeCodes.MultipleChoice, """["a","b"]""")]
    [InlineData(AudienceDataTypeCodes.MultipleChoice, """["Zürich"]""")]
    [InlineData(AudienceDataTypeCodes.MultipleChoice, """["say \"hi\""]""")]
    [InlineData(AudienceDataTypeCodes.Text, "")]
    public void Untyped_OfTyped_IsTheStoredValue(string dataType, string value)
    {
        // Act
        string untyped = AudienceValueJson.Untyped(dataType, AudienceValueJson.Typed(dataType, value));

        // Assert
        untyped.Should().Be(value);
    }

    [Theory]
    [InlineData(AudienceDataTypeCodes.Number, "1.50", "1.5")]
    [InlineData(AudienceDataTypeCodes.Number, "1e3", "1000")]
    [InlineData(AudienceDataTypeCodes.MultipleChoice, """[ "a", "b" ]""", """["a","b"]""")]
    [InlineData(AudienceDataTypeCodes.MultipleChoice, """["🎯"]""", """["\uD83C\uDFAF"]""")]
    public void Untyped_OfTyped_WritesAValueInOneForm(string dataType, string value, string canonical)
    {
        // Act
        string untyped = AudienceValueJson.Untyped(dataType, AudienceValueJson.Typed(dataType, value));

        // Assert
        untyped.Should().Be(canonical);
        JsonNode.DeepEquals(AudienceValueJson.Typed(dataType, untyped), AudienceValueJson.Typed(dataType, value))
            .Should().BeTrue();
    }

    [Theory]
    [InlineData(AudienceDataTypeCodes.Number, "12.50", "12.5")]
    [InlineData(AudienceDataTypeCodes.Number, "\"7\"", "7")]
    [InlineData(AudienceDataTypeCodes.Number, "\"seven\"", "seven")]
    [InlineData(AudienceDataTypeCodes.Boolean, "\"true\"", "true")]
    [InlineData(AudienceDataTypeCodes.Text, "42", "42")]
    [InlineData(AudienceDataTypeCodes.Text, "null", "")]
    public void Untyped_ClientJson_IsTheTextToStore(string dataType, string json, string expected)
    {
        // Act
        string untyped = AudienceValueJson.Untyped(dataType, JsonNode.Parse(json));

        // Assert
        untyped.Should().Be(expected);
    }
}
