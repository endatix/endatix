using System.Text.Json;
using Endatix.Modules.Reporting.Features.FormSchema.Choices;
using Endatix.Modules.Reporting.Features.FormSchema.FormSchema;
using FluentAssertions;

namespace Endatix.Modules.Reporting.Tests.Features.FormSchema.Choices;

/// <summary>
/// Where a select question's choices come from (<see cref="ChoiceSource"/>), with Hub's precedence, and what a single
/// select's codebook entry publishes for each source.
/// </summary>
public sealed class ChoiceSourceTests
{
    [Theory]
    [InlineData("""{"type":"dropdown","name":"q","choices":["a"]}""", "Inline")]
    [InlineData("""{"type":"dropdown","name":"q","edxDataListId":"7","choicesFromQuestion":"s"}""", "DataList")]
    [InlineData("""{"type":"dropdown","name":"q","choicesFromQuestion":"s","edxCarryForwardEnabled":true,"edxCarryForwardSources":["t"]}""", "CarryForward:s")]
    [InlineData("""{"type":"dropdown","name":"q","choicesFromQuestion":"m","choiceValuesFromQuestion":"col"}""", "Unresolvable")]
    [InlineData("""{"type":"dropdown","name":"q","choicesByUrl":{"url":"https://example.com"},"edxCarryForwardEnabled":true,"edxCarryForwardSources":["t"]}""", "Inline")]
    [InlineData("""{"type":"dropdown","name":"q","edxCarryForwardEnabled":true,"edxCarryForwardSources":["t"," ","u"]}""", "CarryForward:t,u")]
    [InlineData("""{"type":"dropdown","name":"q","edxCarryForwardEnabled":false,"edxCarryForwardSources":["t"]}""", "Inline")]
    [InlineData("""{"type":"dropdown","name":"q","edxCarryForwardEnabled":true,"edxCarryForwardSources":[]}""", "Inline")]
    public void Parse_FollowsHubsPrecedence(string questionJson, string expected)
    {
        // Act
        var source = ChoiceSource.Parse(Parse(questionJson));

        // Assert
        Describe(source).Should().Be(expected);
    }

    [Theory]
    [InlineData("""{"type":"radiogroup","name":"q","choicesFromQuestion":"missing"}""")]
    [InlineData("""{"type":"radiogroup","name":"q","choicesFromQuestion":"text"}""")]
    [InlineData("""{"type":"radiogroup","name":"q","choicesFromQuestion":"loop"},{"type":"radiogroup","name":"loop","choicesFromQuestion":"q"}""")]
    [InlineData("""{"type":"radiogroup","name":"q","choicesFromQuestion":"matrix","choiceValuesFromQuestion":"col"}""")]
    [InlineData("""{"type":"radiogroup","name":"q","edxDataListId":"7"}""")]
    public void Compile_SingleSelectWhoseChoicesCannotBeRead_IsMarkedDegradedToText(string questions)
    {
        // Act
        var entry = QuestionEntry(questions, "q");

        // Assert
        entry.GetProperty("exportShape").GetString().Should().Be("categorical");
        entry.GetProperty("degraded").GetString().Should().Be("text");
        entry.GetProperty("choiceSource").GetProperty("kind").GetString().Should().NotBeNullOrEmpty();
        entry.GetProperty("choices").GetArrayLength().Should().Be(0);
    }

    [Fact]
    public void Compile_CopiedChoices_ReadLabelsInEveryLocaleOnTheSource()
    {
        // Act
        var entry = QuestionEntry(
            """{"type":"dropdown","name":"q","choicesFromQuestion":"src"}""",
            "q");

        // Assert
        var label = entry.GetProperty("choices")[0].GetProperty("text");
        label.GetProperty("default").GetString().Should().Be("Apple");
        label.GetProperty("de").GetString().Should().Be("Apfel");
        entry.TryGetProperty("degraded", out _).Should().BeFalse();
    }

    // The source's Other and None are not copied; the destination's own special items still are its own.
    [Fact]
    public void Compile_CopiedChoices_KeepOnlyTheDestinationsSpecialItems()
    {
        // Act
        var entry = QuestionEntry(
            """{"type":"dropdown","name":"q","choicesFromQuestion":"src","showNoneItem":true}""",
            "q");

        // Assert
        entry.GetProperty("choices").EnumerateArray()
            .Select(choice => choice.GetProperty("value").GetString())
            .Should().Equal("apple", "pear", "none");
    }

    [Fact]
    public void Compile_InlineChoices_RecordNoChoiceSource()
    {
        // Act
        var entry = QuestionEntry("""{"type":"dropdown","name":"q","choices":["a"]}""", "q");

        // Assert
        entry.TryGetProperty("choiceSource", out _).Should().BeFalse();
        entry.TryGetProperty("degraded", out _).Should().BeFalse();
    }

    private static JsonElement QuestionEntry(string questions, string name)
    {
        var definition = $$$"""
            {"pages":[{"name":"p","elements":[
              {"type":"checkbox","name":"src","showOtherItem":true,"showNoneItem":true,
               "choices":[{"value":"apple","text":{"default":"Apple","de":"Apfel"}},"pear"]},
              {"type":"text","name":"text"},
              {"type":"matrixdynamic","name":"matrix","columns":[{"name":"col"}]},
              {{{questions}}}]}]}
            """;
        using var codebook = JsonDocument.Parse(new FormSchemaCompiler().CompilePersisted(definition).CodebookJson);
        return codebook.RootElement.GetProperty("questions").GetProperty(name).Clone();
    }

    private static string Describe(ChoiceSource source) =>
        source switch
        {
            ChoiceSource.CarryForward copy => $"CarryForward:{string.Join(',', copy.Sources)}",
            _ => source.GetType().Name,
        };

    private static JsonElement Parse(string json)
    {
        using var document = JsonDocument.Parse(json);
        return document.RootElement.Clone();
    }
}
