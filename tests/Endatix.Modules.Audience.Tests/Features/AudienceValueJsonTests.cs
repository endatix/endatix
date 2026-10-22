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
}
