using System.Text.Json;
using Endatix.Modules.Reporting.Features.FormSchema.Choices;

namespace Endatix.Modules.Reporting.Tests.Features.FormSchema.Choices;

/// <summary>
/// The values a SurveyJS select question can store, read from its definition (survey-core
/// <c>question_baseselect.ts</c>, <c>settings.ts</c> special item values).
/// </summary>
public sealed class ChoiceSetTests
{
    [Fact]
    public void Read_EverySpecialItem_OrdersInlineThenOtherNoneRefusedDontKnow()
    {
        var set = Read("""
            {
              "type": "checkbox", "name": "q",
              "choices": ["a", "b"],
              "showDontKnowItem": true, "showRefuseItem": true, "showNoneItem": true, "showOtherItem": true
            }
            """);

        set.Items.Select(item => (item.Value, item.Kind)).Should().Equal(
            ("a", ChoiceKind.Choice),
            ("b", ChoiceKind.Choice),
            ("other", ChoiceKind.Other),
            ("none", ChoiceKind.None),
            ("refused", ChoiceKind.Refused),
            ("dontknow", ChoiceKind.DontKnow));
        set.IsMultiple.Should().BeTrue();
    }

    [Theory]
    [InlineData("""{"type":"radiogroup","name":"q","choices":["a"],"showNoneItem":true}""", "a,none")]
    [InlineData("""{"type":"dropdown","name":"q","choices":["a"],"showRefuseItem":true}""", "a,refused")]
    [InlineData("""{"type":"tagbox","name":"q","choices":["a"],"showDontKnowItem":true}""", "a,dontknow")]
    [InlineData("""{"type":"checkbox","name":"q","choices":["a"],"hasOther":true,"hasNone":true}""", "a,other,none")]
    [InlineData("""{"type":"checkbox","name":"q","choices":["a"],"showSelectAllItem":true}""", "a")]
    [InlineData("""{"type":"checkbox","name":"q","choices":["a"],"showNoneItem":false}""", "a")]
    public void Read_SpecialFlagsAndAliases_GiveTheStoredValues(string question, string expected)
    {
        Read(question).Items.Select(item => item.Value).Should().Equal(expected.Split(','));
    }

    [Theory]
    [InlineData("radiogroup", false)]
    [InlineData("dropdown", false)]
    [InlineData("checkbox", true)]
    [InlineData("tagbox", true)]
    public void Read_SelectTypes_SetIsMultiple(string type, bool multiple)
    {
        Read($$"""{"type":"{{type}}","name":"q","choices":["a"]}""").IsMultiple.Should().Be(multiple);
    }

    [Theory]
    [InlineData("""{"type":"ranking","name":"q","choices":["a"]}""")]
    [InlineData("""{"type":"text","name":"q"}""")]
    [InlineData("""{"type":"boolean","name":"q"}""")]
    public void Read_NotABaseSelect_ReturnsNull(string question)
    {
        using var document = JsonDocument.Parse(question);

        ChoiceSet.Read(document.RootElement).Should().BeNull();
    }

    [Fact]
    public void Read_ImagePicker_KeepsOtherButNoNoneRefusedOrDontKnow()
    {
        var set = Read("""
            {"type":"imagepicker","name":"q","choices":["a"],"showOtherItem":true,"showNoneItem":true,"showRefuseItem":true}
            """);

        set.Items.Select(item => item.Value).Should().Equal("a", "other");
    }

    [Fact]
    public void Read_SpecialItems_AreExclusiveAndInlineChoicesFollowIsExclusive()
    {
        var set = Read("""
            {
              "type": "checkbox", "name": "q",
              "choices": ["a", { "value": "b", "isExclusive": true }],
              "showOtherItem": true, "showNoneItem": true, "showRefuseItem": true, "showDontKnowItem": true
            }
            """);

        set.Items.Select(item => (item.Value, item.Kind, item.Exclusive)).Should().Equal(
            ("a", ChoiceKind.Choice, false),
            ("b", ChoiceKind.Choice, true),
            ("other", ChoiceKind.Other, false),
            ("none", ChoiceKind.None, true),
            ("refused", ChoiceKind.Refused, true),
            ("dontknow", ChoiceKind.DontKnow, true));
    }

    [Fact]
    public void Read_SpecialItemLabels_ComeFromTheTextPropertyOrTheSurveyJsDefault()
    {
        var set = Read("""
            {
              "type": "radiogroup", "name": "q", "choices": ["a"],
              "showOtherItem": true, "showNoneItem": true, "noneText": "Nothing",
              "showRefuseItem": true, "showDontKnowItem": true
            }
            """);

        set.Items.Select(item => (item.Value, item.FallbackLabel, item.LabelProperty)).Should().Equal(
            ("a", "a", "text"),
            ("other", "Other", "otherText"),
            ("none", "Nothing", "noneText"),
            ("refused", "Refuse to answer", "refuseText"),
            ("dontknow", "Don't know", "dontKnowText"));
    }

    [Fact]
    public void LoopDriverValues_AreInlineChoicesAndOtherOnly()
    {
        var set = Read("""
            {"type":"checkbox","name":"q","choices":["a","b"],"showOtherItem":true,"showNoneItem":true,"showRefuseItem":true}
            """);

        set.LoopDriverValues.Should().Equal("a", "b", "other");
    }

    // survey-core question_baseselect.ts:2068-2077 (getStoreOthersAsComment) and survey.ts:1920-1927.
    // question_baseselecttests.ts:2576,1506 (comment mode), surveytests.ts:6798-6838 (value mode).
    [Theory]
    [InlineData("""{"showOtherItem":true}""", true, "Comment")]
    [InlineData("""{"showOtherItem":true}""", false, "Value")]
    [InlineData("""{"showOtherItem":true,"storeOthersAsComment":false}""", true, "Value")]
    [InlineData("""{"showOtherItem":true,"storeOthersAsComment":true}""", false, "Comment")]
    [InlineData("""{"showOtherItem":true,"storeOthersAsComment":"default"}""", false, "Value")]
    [InlineData("""{"showOtherItem":true,"storeOthersAsComment":"default"}""", true, "Comment")]
    [InlineData("""{"showOtherItem":true,"showCommentArea":true}""", true, "Value")]
    [InlineData("""{"showOtherItem":true,"hasComment":true,"storeOthersAsComment":true}""", true, "Value")]
    [InlineData("""{"showOtherItem":true,"storeOthersAsComment":false,"choicesByUrl":{"url":"x"}}""", true, "Comment")]
    [InlineData("""{"showOtherItem":true,"showCommentArea":true,"choicesByUrl":{"url":"x"}}""", true, "Value")]
    [InlineData("""{"showOtherItem":true,"storeOthersAsComment":false,"choices":[{"value":"a","showCommentArea":true}]}""", true, "Comment")]
    public void Read_OtherStorage_FollowsSurveyJsPrecedence(string questionProperties, bool surveyStoresComment, string expected)
    {
        using var properties = JsonDocument.Parse(questionProperties);
        Dictionary<string, JsonElement> question = new(StringComparer.Ordinal)
        {
            ["type"] = JsonDocument.Parse("\"radiogroup\"").RootElement,
            ["name"] = JsonDocument.Parse("\"q\"").RootElement,
            ["choices"] = JsonDocument.Parse("""["a"]""").RootElement,
        };
        foreach (var property in properties.RootElement.EnumerateObject())
        {
            question[property.Name] = property.Value;
        }

        var set = Read(JsonSerializer.Serialize(question), new SurveyChoiceDefaults(surveyStoresComment));

        set.OtherStorage.Should().Be(Enum.Parse<OtherStorage>(expected));
    }

    // Typed text in the value is told apart from a choice only when every choice is on the form. Choices copied from
    // another question or loaded later are not, so a value the catalog lacks may be a real choice: keep it.
    [Theory]
    [InlineData("""{"type":"radiogroup","name":"q","choicesFromQuestion":"src","showOtherItem":true,"storeOthersAsComment":false}""")]
    [InlineData("""{"type":"radiogroup","name":"q","choices":["a"],"choicesFromQuestion":"src","showOtherItem":true,"storeOthersAsComment":false}""")]
    [InlineData("""{"type":"checkbox","name":"q","choiceValuesFromQuestion":"src","showOtherItem":true,"storeOthersAsComment":false}""")]
    [InlineData("""{"type":"tagbox","name":"q","showOtherItem":true,"storeOthersAsComment":false}""")]
    [InlineData("""{"type":"dropdown","name":"q","choices":["a"],"allowCustomChoices":true,"showOtherItem":true,"storeOthersAsComment":false}""")]
    public void Read_ChoicesNotOnTheForm_NeverReadsTheValueAsOtherText(string question)
    {
        Read(question).OtherStorage.Should().Be(OtherStorage.Comment);
    }

    [Fact]
    public void Read_NoOtherItem_HasNoOtherStorage()
    {
        Read("""{"type":"radiogroup","name":"q","choices":["a"],"storeOthersAsComment":false}""")
            .OtherStorage.Should().BeNull();
    }

    [Theory]
    [InlineData("""{"pages":[]}""", true)]
    [InlineData("""{"storeOthersAsComment":false,"pages":[]}""", false)]
    [InlineData("""{"storeOthersAsComment":true,"pages":[]}""", true)]
    public void SurveyChoiceDefaults_Of_ReadsTheSurveyLevelSetting(string definition, bool expected)
    {
        using var document = JsonDocument.Parse(definition);

        SurveyChoiceDefaults.Of(document.RootElement).StoreOthersAsComment.Should().Be(expected);
    }

    private static ChoiceSet Read(string question, SurveyChoiceDefaults? survey = null)
    {
        using var document = JsonDocument.Parse(question);
        var set = ChoiceSet.Read(document.RootElement.Clone(), survey ?? SurveyChoiceDefaults.Default);
        set.Should().NotBeNull();
        return set!;
    }
}
