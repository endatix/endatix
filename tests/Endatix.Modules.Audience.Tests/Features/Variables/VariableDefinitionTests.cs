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

    [Theory]
    [InlineData(AudienceDataTypeCodes.Text, """[{"type":"text"},{"type":"comment"}]""")]
    [InlineData(
        AudienceDataTypeCodes.Number,
        """[{"type":"text","inputType":"number"},{"type":"rating"},{"type":"slider"}]""")]
    [InlineData(AudienceDataTypeCodes.Boolean, """[{"type":"boolean"}]""")]
    [InlineData(AudienceDataTypeCodes.Date, """[{"type":"text","inputType":"date"}]""")]
    [InlineData(
        AudienceDataTypeCodes.DateTime,
        """[{"type":"text"},{"type":"text","inputType":"datetime-local"}]""")]
    [InlineData(
        AudienceDataTypeCodes.SingleChoice,
        """[{"type":"dropdown"},{"type":"radiogroup"},{"type":"buttongroup"}]""")]
    [InlineData(AudienceDataTypeCodes.MultipleChoice, """[{"type":"checkbox"},{"type":"tagbox"}]""")]
    public void Build_EachDataType_NamesTheQuestionTypesItCanPrefill(string dataType, string expectedTargets)
    {
        // Act
        JsonObject definition = VariableDefinition.Build([PropertyOf(dataType, ChoicesFor(dataType))]);

        // Assert
        JsonNode? targets = definition["elements"]![0]!["prefillTargets"];
        JsonNode.DeepEquals(targets, JsonNode.Parse(expectedTargets)).Should().BeTrue(targets?.ToJsonString());
    }

    [Theory]
    [InlineData(AudienceDataTypeCodes.SingleChoice)]
    [InlineData(AudienceDataTypeCodes.MultipleChoice)]
    public void Build_ChoiceThatAllowsOther_PrefillsTheSameQuestionTypes(string dataType)
    {
        // Act
        JsonObject closed = VariableDefinition.Build([PropertyOf(dataType, """["basic"]""")]);
        JsonObject open = VariableDefinition.Build([PropertyOf(dataType, """["basic"]""", allowsOther: true)]);

        // Assert
        JsonNode.DeepEquals(closed["elements"]![0]!["prefillTargets"], open["elements"]![0]!["prefillTargets"])
            .Should().BeTrue();
    }

    [Fact]
    public void Build_EveryDataType_PrefillsAtLeastItsOwnQuestion()
    {
        foreach (string dataType in AudienceDataTypeCodes.All)
        {
            // Act
            JsonNode question = VariableDefinition.Build([PropertyOf(dataType, ChoicesFor(dataType))])["elements"]![0]!;

            // Assert
            question["prefillTargets"]!.AsArray()
                .Select(target => (Type: (string?)target!["type"], InputType: (string?)target["inputType"]))
                .Should().Contain(((string?)question["type"], (string?)question["inputType"]), dataType);
        }
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

    private static string? ChoicesFor(string dataType) =>
        dataType is AudienceDataTypeCodes.SingleChoice or AudienceDataTypeCodes.MultipleChoice ? """["basic"]""" : null;

    /// <summary>The question as SurveyJS reads it; <c>prefillTargets</c> has its own tests.</summary>
    private static void AssertQuestions(JsonObject definition, string expectedQuestion)
    {
        definition.Select(entry => entry.Key).Should().Equal("elements");
        definition["elements"]!.AsArray().Should().HaveCount(1);
        JsonObject question = definition["elements"]![0]!.AsObject().DeepClone().AsObject();
        question.Remove("prefillTargets");
        JsonNode.DeepEquals(question, JsonNode.Parse(expectedQuestion)).Should().BeTrue(question.ToJsonString());
    }
}
