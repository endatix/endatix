using System.Text.Json.Nodes;
using Endatix.Modules.Audience.Contracts;
using Endatix.Modules.Audience.Features;

namespace Endatix.Modules.Audience.Tests.Features;

public sealed class AudienceValueJsonTests
{
    [Theory]
    [InlineData(AudienceDataTypeCodes.Text, "Columbus", "\"Columbus\"")]
    [InlineData(AudienceDataTypeCodes.Text, "42", "\"42\"")]
    [InlineData(AudienceDataTypeCodes.Number, "12", "12")]
    [InlineData(AudienceDataTypeCodes.Number, "-0.5", "-0.5")]
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

    /// <summary>The write-back stores what the preview showed: each canonical stored value comes back unchanged.</summary>
    [Theory]
    [InlineData(AudienceDataTypeCodes.Text, "03. MIDWEST - COLUMBUS")]
    [InlineData(AudienceDataTypeCodes.Text, "true")]
    [InlineData(AudienceDataTypeCodes.Number, "12")]
    [InlineData(AudienceDataTypeCodes.Number, "-0.5")]
    [InlineData(AudienceDataTypeCodes.Boolean, "true")]
    [InlineData(AudienceDataTypeCodes.Boolean, "false")]
    [InlineData(AudienceDataTypeCodes.Date, "2026-10-21")]
    [InlineData(AudienceDataTypeCodes.DateTime, "2026-10-21T09:30:00+03:00")]
    [InlineData(AudienceDataTypeCodes.SingleChoice, "other text")]
    [InlineData(AudienceDataTypeCodes.MultipleChoice, """["a","Ünïcode key"]""")]
    [InlineData(AudienceDataTypeCodes.MultipleChoice, "[]")]
    [InlineData(AudienceDataTypeCodes.Number, "")]
    public void Untyped_OfTyped_IsTheStoredValue(string dataType, string value)
    {
        AudienceValueJson.Untyped(AudienceValueJson.Typed(dataType, value)).Should().Be(value);
    }

    /// <summary>
    /// A number or list stored in another spelling (<c>1.50</c>, spaced JSON) comes back canonical; typing it again gives
    /// the same JSON, so the respondent reads the same value.
    /// </summary>
    [Theory]
    [InlineData(AudienceDataTypeCodes.Number, "1.50", "1.5")]
    [InlineData(AudienceDataTypeCodes.Number, "1e2", "100")]
    [InlineData(AudienceDataTypeCodes.MultipleChoice, """[ "a", "b" ]""", """["a","b"]""")]
    public void Untyped_OfTyped_NonCanonicalValue_TypesTheSame(string dataType, string value, string canonical)
    {
        // Act
        string untyped = AudienceValueJson.Untyped(AudienceValueJson.Typed(dataType, value));

        // Assert
        untyped.Should().Be(canonical);
        JsonNode.DeepEquals(AudienceValueJson.Typed(dataType, untyped), AudienceValueJson.Typed(dataType, value))
            .Should().BeTrue();
    }
}
