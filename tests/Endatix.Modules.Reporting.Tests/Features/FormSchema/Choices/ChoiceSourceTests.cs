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
    [InlineData("""{"type":"dropdown","name":"q","choicesByUrl":{"url":"https://example.com"},"edxCarryForwardEnabled":true,"edxCarryForwardSources":["t"]}""", "Unresolvable")]
    [InlineData("""{"type":"dropdown","name":"q","choicesByUrl":{"url":""},"choices":["a"]}""", "Inline")]
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
    [InlineData("missing")]
    [InlineData("text")]
    [InlineData("list")]
    [InlineData("url")]
    [InlineData("values")]
    [InlineData("loop")]
    public void Compile_CopyFromASourceTheCodebookCannotRead_IsMarkedDegradedToText(string source)
    {
        // Act
        var entry = QuestionEntry($$"""{"type":"radiogroup","name":"q","choicesFromQuestion":"{{source}}"}""", "q");

        // Assert
        entry.GetProperty("exportShape").GetString().Should().Be("categorical");
        entry.GetProperty("degraded").GetString().Should().Be("text");
        ChoiceSourceJson(entry).Should().Be($$"""{"kind":"carryForward","sources":["{{source}}"]}""");
        entry.GetProperty("choices").GetArrayLength().Should().Be(0);
    }

    // Only a copy changes. A question whose own choices come from a list, a URL or answers keeps today's output (#1160
    // resolves data lists) and records where they come from.
    [Theory]
    [InlineData("list", """{"kind":"dataList","listId":"7"}""")]
    [InlineData("url", """{"kind":"unresolvable","reason":"choicesByUrl"}""")]
    [InlineData("values", """{"kind":"unresolvable","reason":"choiceValuesFromQuestion"}""")]
    public void Compile_QuestionNotCopyingChoices_KeepsItsOwnChoicesAndRecordsTheSource(string name, string choiceSource)
    {
        // Act
        var entry = QuestionEntry("""{"type":"radiogroup","name":"q","choices":["a"]}""", name);

        // Assert
        ChoiceSourceJson(entry).Should().Be(choiceSource);
        entry.TryGetProperty("degraded", out _).Should().BeFalse();
        entry.GetProperty("choices").EnumerateArray().Select(choice => choice.GetProperty("value").GetString())
            .Should().Equal("own");
    }

    [Fact]
    public void Compile_CopiedChoices_ReadLabelsInEveryLocaleOnTheSource()
    {
        // Act
        var entry = QuestionEntry("""{"type":"dropdown","name":"q","choicesFromQuestion":"src"}""", "q");

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
        Values(entry).Should().Equal("apple", "pear", "none");
    }

    [Fact]
    public void Compile_TwoCopiesOfTheSameSource_PublishItsChoicesOnce()
    {
        // Act
        var entry = QuestionEntry(
            """
            {"type":"dropdown","name":"left","choicesFromQuestion":"src"},
            {"type":"dropdown","name":"right","choicesFromQuestion":"src"},
            {"type":"radiogroup","name":"q","edxCarryForwardEnabled":true,"edxCarryForwardSources":["left","right","src"]}
            """,
            "q");

        // Assert
        Values(entry).Should().Equal("apple", "pear");
    }

    // Hub drops a source that no longer exists or is not a select; the others still carry forward.
    [Fact]
    public void Compile_CarryForwardWithAMissingAndATextSource_PublishesTheOtherSources()
    {
        // Act
        var entry = QuestionEntry(
            """{"type":"radiogroup","name":"q","edxCarryForwardEnabled":true,"edxCarryForwardSources":["deleted","text","src"]}""",
            "q");

        // Assert
        Values(entry).Should().Equal("apple", "pear");
        entry.TryGetProperty("degraded", out _).Should().BeFalse();
    }

    [Fact]
    public void Compile_DegradedCopy_KeepsTheDestinationsOwnSpecialItems()
    {
        // Act
        var entry = QuestionEntry("""{"type":"radiogroup","name":"q","choicesFromQuestion":"list","showOtherItem":true}""", "q");

        // Assert
        entry.GetProperty("degraded").GetString().Should().Be("text");
        Values(entry).Should().Equal("other");
    }

    // Each copy carries forward from the two before it, so an unshared walk would take 2^40 steps.
    [Fact(Timeout = 10_000)]
    public async Task Compile_LongChainOfCopies_ResolvesEachSourceOnce()
    {
        // Arrange
        var copies = Enumerable.Range(2, 40).Select(level =>
            $$"""{"type":"dropdown","name":"c{{level}}","edxCarryForwardEnabled":true,"edxCarryForwardSources":["c{{level - 1}}","c{{level - 2}}"]}""");
        var questions = string.Join(",", ["""{"type":"dropdown","name":"c0","choices":["a"]}""", """{"type":"dropdown","name":"c1","choices":["b"]}""", .. copies]);

        // Act
        var entry = await Task.Run(() => QuestionEntry(questions, "c41"), TestContext.Current.CancellationToken);

        // Assert
        Values(entry).Should().Equal("b", "a");
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

    // Sources the theories copy from: own choices, text, a data list, a URL list, values from answers, and a cycle.
    private static JsonElement QuestionEntry(string questions, string name)
    {
        var definition = $$$"""
            {"pages":[{"name":"p","elements":[
              {"type":"checkbox","name":"src","showOtherItem":true,"showNoneItem":true,
               "choices":[{"value":"apple","text":{"default":"Apple","de":"Apfel"}},"pear"]},
              {"type":"text","name":"text"},
              {"type":"dropdown","name":"list","edxDataListId":"7","choices":["own"]},
              {"type":"dropdown","name":"url","choicesByUrl":{"url":"https://example.com/items"},"choices":["own"]},
              {"type":"matrixdynamic","name":"matrix","columns":[{"name":"col"}]},
              {"type":"dropdown","name":"values","choicesFromQuestion":"matrix","choiceValuesFromQuestion":"col","choices":["own"]},
              {"type":"dropdown","name":"loop","choicesFromQuestion":"q"},
              {{{questions}}}]}]}
            """;
        using var codebook = JsonDocument.Parse(new FormSchemaCompiler().CompilePersisted(definition).CodebookJson);
        return codebook.RootElement.GetProperty("questions").GetProperty(name).Clone();
    }

    private static string ChoiceSourceJson(JsonElement entry) => entry.GetProperty("choiceSource").GetRawText();

    private static IEnumerable<string?> Values(JsonElement entry) =>
        entry.GetProperty("choices").EnumerateArray().Select(choice => choice.GetProperty("value").GetString());

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
