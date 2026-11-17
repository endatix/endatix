using System.Text.Json;
using Endatix.Modules.Reporting.Features.FormSchema.FlattenedFormDefinition;
using Endatix.Modules.Reporting.Features.FormSchema.FormSchema;
using FluentAssertions;

namespace Endatix.Modules.Reporting.Tests.Features.FormSchema.FlattenedFormDefinition;

/// <summary>
/// The iterations a question loop (<c>loopSource</c>) compiles to: one per choice its source can offer, its own or
/// copied, for any select or a ranking.
/// </summary>
public sealed class LoopSourceDriverTests
{
    // #1555 (v0.9.1) dropped these columns: up to v0.9.0 a ranking with its own choices drove a loop.
    [Fact]
    public void Flatten_LoopOverARankingWithItsOwnChoices_RepeatsOncePerChoice()
    {
        // Arrange
        var definition = Definition("""
            {"type":"ranking","name":"source","choices":["a","b"]}
            """);

        // Act
        var columns = Flatten(definition);

        // Assert
        LoopKeys(columns).Should().Equal("loop__a__q", "loop__b__q");
    }

    [Fact]
    public void Flatten_LoopOverAChainedCopy_RepeatsOncePerChoiceOfTheQuestionAtTheRootOfTheChain()
    {
        // Arrange
        var definition = Definition("""
            {"type":"checkbox","name":"brands","choices":["a","b","c"]},
            {"type":"checkbox","name":"seen","choicesFromQuestion":"brands","choicesFromQuestionMode":"selected"},
            {"type":"checkbox","name":"source","choicesFromQuestion":"seen","choicesFromQuestionMode":"selected"}
            """);

        // Act
        var columns = Flatten(definition);

        // Assert
        LoopKeys(columns).Should().Equal("loop__a__q", "loop__b__q", "loop__c__q");
    }

    // Hub carry forward offers the union of its sources in source order, the first occurrence of a value winning.
    [Fact]
    public void Flatten_LoopOverACarryForwardFromTwoSources_RepeatsOverTheUnionInSourceOrder()
    {
        // Arrange
        var definition = Definition("""
            {"type":"checkbox","name":"fruit","choices":["apple","pear"]},
            {"type":"ranking","name":"orchard","choices":["pear","plum"]},
            {"type":"checkbox","name":"source","edxCarryForwardEnabled":true,"edxCarryForwardSources":["orchard","fruit"]}
            """);

        // Act
        var columns = Flatten(definition);

        // Assert
        LoopKeys(columns).Should().Equal("loop__pear__q", "loop__plum__q", "loop__apple__q");
    }

    // A copy publishes its sources' choices, so choices the Creator left on it (item1..3) no longer drive panels: the
    // stored panels carry the copied values. Under a merge the old leftover columns stay, empty.
    [Fact]
    public void Flatten_LoopOverACopyWithLeftoverChoices_RepeatsOverTheCopiedChoicesOnly()
    {
        // Arrange
        var definition = Definition("""
            {"type":"checkbox","name":"brands","choices":["a","b"]},
            {"type":"checkbox","name":"source","choicesFromQuestion":"brands","choices":["item1","item2"]}
            """);

        // Act
        var columns = Flatten(definition);

        // Assert
        LoopKeys(columns).Should().Equal("loop__a__q", "loop__b__q");
    }

    // The copy's own Other is an item the respondent can pick, so it gets a panel, as an inline source's Other does.
    [Fact]
    public void Flatten_LoopOverACopyThatShowsOther_RepeatsOverTheCopiedChoicesThenOther()
    {
        // Arrange
        var definition = Definition("""
            {"type":"checkbox","name":"brands","choices":["a","b"]},
            {"type":"checkbox","name":"source","choicesFromQuestion":"brands","showOtherItem":true,"showNoneItem":true}
            """);

        // Act
        var columns = Flatten(definition);

        // Assert
        LoopKeys(columns).Should().Equal("loop__a__q", "loop__b__q", "loop__other__q");
    }

    [Fact]
    public void Flatten_LoopOverACopyOnACycle_HasNoIterations()
    {
        // Arrange
        var definition = Definition("""
            {"type":"checkbox","name":"other","choicesFromQuestion":"source"},
            {"type":"checkbox","name":"source","choicesFromQuestion":"other"}
            """);

        // Act
        var columns = Flatten(definition);

        // Assert
        LoopKeys(columns).Should().BeEmpty();
    }

    // A question that is neither a select nor a ranking drives no panels, even with a choices property.
    [Fact]
    public void Flatten_LoopOverAMatrixWithChoices_HasNoIterations()
    {
        // Arrange
        var definition = Definition("""
            {"type":"matrixdropdown","name":"source","choices":["a","b"],"rows":["r"],"columns":[{"name":"c"}]}
            """);

        // Act
        var columns = Flatten(definition);

        // Assert
        LoopKeys(columns).Should().BeEmpty();
    }

    // The limit counts every value the copy offers, chained sources included, and names the loop's source.
    [Fact]
    public void Flatten_LoopOverACopyOfferingMoreThanTheChoiceLimit_Throws()
    {
        // Arrange
        var definition = Definition("""
            {"type":"checkbox","name":"fruit","choices":["apple","pear"]},
            {"type":"ranking","name":"orchard","choices":["plum"]},
            {"type":"checkbox","name":"source","edxCarryForwardEnabled":true,"edxCarryForwardSources":["orchard","fruit"]}
            """);
        using var document = JsonDocument.Parse(definition);
        SchemaCompilationLimits limits = new() { MaxChoicesPerQuestion = 2 };

        // Act
        Action act = () => FormDefinitionFlattener.Flatten(document.RootElement, limits);

        // Assert
        var exception = act.Should().Throw<SchemaCompilationLimitExceededException>().Which;
        exception.LimitKind.Should().Be(SchemaCompilationLimitKind.MaxChoicesPerQuestion);
        exception.Context.Should().Be("source");
    }

    private static string Definition(string sourceQuestions) =>
        $$"""
        {"pages":[{"name":"p1","elements":[{{sourceQuestions}},
          {"type":"paneldynamic","name":"loop","loopSource":["source"],"templateElements":[{"type":"text","name":"q"}]}]}]}
        """;

    private static IReadOnlyList<FormSchemaColumn> Flatten(string definitionJson)
    {
        using var definition = JsonDocument.Parse(definitionJson);
        return FormDefinitionFlattener.Flatten(definition.RootElement);
    }

    private static List<string> LoopKeys(IEnumerable<FormSchemaColumn> columns) =>
        columns.Where(column => column.Kind == FormSchemaColumnKind.LoopSource).Select(column => column.Key).ToList();
}
