using System.Text.Json.Nodes;
using Endatix.Modules.Audience.Contracts;
using Endatix.Modules.Audience.Domain;
using Endatix.Modules.Audience.Features.Variables;

namespace Endatix.Modules.Audience.Tests.Features.Variables;

public sealed class VariableDefinitionTests
{
    [Theory]
    [InlineData(AudienceDataTypeCodes.Text, """{"type":"text","name":"plan_tier","title":"Plan tier"}""")]
    [InlineData(
        AudienceDataTypeCodes.Number,
        """{"type":"text","name":"plan_tier","title":"Plan tier","inputType":"number"}""")]
    [InlineData(AudienceDataTypeCodes.Boolean, """{"type":"boolean","name":"plan_tier","title":"Plan tier"}""")]
    [InlineData(
        AudienceDataTypeCodes.Date,
        """{"type":"text","name":"plan_tier","title":"Plan tier","inputType":"date"}""")]
    [InlineData(AudienceDataTypeCodes.DateTime, """{"type":"text","name":"plan_tier","title":"Plan tier"}""")]
    public void Build_ValueProperty_IsOneQuestionOfItsType(string dataType, string expectedQuestion)
    {
        // Act
        JsonObject definition = VariableDefinition.Build([PropertyOf(dataType)]);

        // Assert
        AssertQuestions(definition, expectedQuestion);
    }

    [Theory]
    [InlineData(
        AudienceDataTypeCodes.SingleChoice,
        false,
        """{"type":"dropdown","name":"plan_tier","title":"Plan tier","choices":["basic","pro"]}""")]
    [InlineData(
        AudienceDataTypeCodes.SingleChoice,
        true,
        """{"type":"dropdown","name":"plan_tier","title":"Plan tier","choices":["basic","pro"],"allowCustomChoices":true}""")]
    [InlineData(
        AudienceDataTypeCodes.MultipleChoice,
        false,
        """{"type":"checkbox","name":"plan_tier","title":"Plan tier","choices":["basic","pro"]}""")]
    [InlineData(
        AudienceDataTypeCodes.MultipleChoice,
        true,
        """{"type":"tagbox","name":"plan_tier","title":"Plan tier","choices":["basic","pro"],"allowCustomChoices":true}""")]
    public void Build_ChoiceProperty_ListsItsKeysAndTakesFreeTextWhenItAllowsOther(
        string dataType,
        bool allowsOther,
        string expectedQuestion)
    {
        // Act
        JsonObject definition = VariableDefinition.Build(
            [PropertyOf(dataType, """["basic","pro"]""", allowsOther)]);

        // Assert
        AssertQuestions(definition, expectedQuestion);
    }

    [Fact]
    public void Build_SeveralProperties_KeepsTheirOrder()
    {
        // Act
        JsonObject definition = VariableDefinition.Build(
        [
            new Property(new PropertyCreateArgs(1, 10, "City", AudienceDataTypeCodes.Text, 0)),
            new Property(new PropertyCreateArgs(1, 10, "Age", AudienceDataTypeCodes.Number, 1)),
        ]);

        // Assert
        definition["elements"]!.AsArray().Select(question => question!["name"]!.GetValue<string>())
            .Should().Equal("city", "age");
    }

    private static Property PropertyOf(string dataType, string? choicesJson = null, bool allowsOther = false) =>
        new(new PropertyCreateArgs(1, 10, "Plan tier", dataType, 0, choicesJson, allowsOther));

    private static void AssertQuestions(JsonObject definition, string expectedQuestion) =>
        JsonNode.DeepEquals(definition, JsonNode.Parse($$"""{"elements":[{{expectedQuestion}}]}"""))
            .Should().BeTrue(definition.ToJsonString());
}
