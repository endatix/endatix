using System.Text.Json;
using Endatix.Modules.Reporting.Features.FormSchema.FlattenedFormDefinition;
using Endatix.Modules.Reporting.Features.FormSchema.FormSchema;

namespace Endatix.Modules.Reporting.Tests.Features.FormSchema.FlattenedFormDefinition;

/// <summary>
/// A ranking that copies its choices (<c>choicesFromQuestion</c> or Hub carry forward) compiles to one rank column per
/// choice its sources offer, whatever the mode, and its codebook lists those choices with the source's labels.
/// </summary>
public sealed class RankingCopiedChoicesTests
{
    private const string SOURCE = """
        {"type":"checkbox","name":"brands","choices":[{"value":"a","text":{"default":"Apple","es":"Manzana"}},{"value":"b","text":"Pear"},"c"]}
        """;

    public static TheoryData<string> Copies => new()
    {
        """ "choicesFromQuestion":"brands","choicesFromQuestionMode":"all" """,
        """ "choicesFromQuestion":"brands","choicesFromQuestionMode":"selected","selectToRankEnabled":true """,
        """ "choicesFromQuestion":"brands","choicesFromQuestionMode":"unselected" """,
        """ "edxCarryForwardEnabled":true,"edxCarryForwardSources":["brands"],"edxCarryForwardMode":"all" """,
        """ "edxCarryForwardEnabled":true,"edxCarryForwardSources":["brands"],"edxCarryForwardMode":"selected" """,
        """ "edxCarryForwardEnabled":true,"edxCarryForwardSources":["brands"],"edxCarryForwardMode":"unselected" """,
    };

    [Theory]
    [MemberData(nameof(Copies))]
    public void Flatten_TopLevelRankingCopy_HasOneColumnPerSourceChoiceInEveryMode(string copy)
    {
        // Act
        var columns = Flatten(TopLevel(Ranking(copy)));

        // Assert
        RankingKeys(columns).Should().Equal("rank__a", "rank__b", "rank__c");
        RankingColumn(columns, "rank__b").Label.Should().Be("rank — Pear");
    }

    [Theory]
    [MemberData(nameof(Copies))]
    public void Flatten_RankingCopyInsideALoop_HasOneColumnPerIterationAndSourceChoiceInEveryMode(string copy)
    {
        // Act
        var columns = Flatten(InLoop(Ranking(copy)));

        // Assert
        RankingKeys(columns).Should().Equal(
            "loop__x__rank__a", "loop__x__rank__b", "loop__x__rank__c",
            "loop__y__rank__a", "loop__y__rank__b", "loop__y__rank__c");
    }

    [Theory]
    [MemberData(nameof(Copies))]
    public void CompilePersisted_TopLevelRankingCopy_ListsTheSourceChoicesWithTheirLabels(string copy)
    {
        // Act
        var codebook = Codebook(TopLevel(Ranking(copy)));

        // Assert
        Entries(codebook.GetProperty("questions").GetProperty("rank")).Should().Equal(_expectedChoices);
        Entries(codebook.GetProperty("choiceCatalogs").GetProperty("rank")).Should().Equal(_expectedChoices);
        ChoiceLabel(codebook, "rank__a").Should().Be("""{"default":"Apple","es":"Manzana"}""");
        ChoiceLabel(codebook, "rank__c").Should().Be("""{"default":"c"}""");
    }

    [Theory]
    [MemberData(nameof(Copies))]
    public void CompilePersisted_RankingCopyInsideALoop_ListsTheSourceChoicesWithTheirLabels(string copy)
    {
        // Act
        var codebook = Codebook(InLoop(Ranking(copy)));

        // Assert
        Entries(codebook.GetProperty("questions").GetProperty("rank")).Should().Equal(_expectedChoices);
        ChoiceLabel(codebook, "loop__y__rank__b").Should().Be("""{"default":"Pear"}""");
    }

    // Hub publishes the choices of the question at the root of the chain, read with that question's labels.
    [Fact]
    public void CompilePersisted_RankingCopyOfACopy_UsesTheRootQuestionsChoicesAndLabels()
    {
        // Arrange
        var definition = TopLevel("""
            {"type":"checkbox","name":"seen","choicesFromQuestion":"brands","choicesFromQuestionMode":"selected"},
            {"type":"ranking","name":"rank","choicesFromQuestion":"seen","choicesFromQuestionMode":"selected"}
            """);

        // Act
        var columns = Flatten(definition);
        var codebook = Codebook(definition);

        // Assert
        RankingKeys(columns).Should().Equal("rank__a", "rank__b", "rank__c");
        Entries(codebook.GetProperty("questions").GetProperty("rank")).Should().Equal(_expectedChoices);
    }

    // Creator leaves its default items on a question that copies its choices; the respondent never sees them.
    [Fact]
    public void Flatten_RankingCopyWithLeftoverChoices_HasColumnsForTheCopiedChoicesOnly()
    {
        // Act
        var columns = Flatten(TopLevel(Ranking(""" "choicesFromQuestion":"brands","choices":["item1","item2"] """)));

        // Assert
        RankingKeys(columns).Should().Equal("rank__a", "rank__b", "rank__c");
    }

    // Hub carry forward offers the union of its sources in source order, the first occurrence of a value winning.
    [Fact]
    public void CompilePersisted_RankingCopyFromTwoSources_RanksTheUnionInSourceOrder()
    {
        // Arrange
        var definition = TopLevel("""
            {"type":"ranking","name":"orchard","choices":[{"value":"b","text":"Pear (orchard)"},"z"]},
            {"type":"ranking","name":"rank","edxCarryForwardEnabled":true,"edxCarryForwardSources":["orchard","brands"]}
            """);

        // Act
        var columns = Flatten(definition);
        var codebook = Codebook(definition);

        // Assert
        RankingKeys(columns).Where(key => key.StartsWith("rank__", StringComparison.Ordinal))
            .Should().Equal("rank__b", "rank__z", "rank__a", "rank__c");
        ChoiceLabel(codebook, "rank__b").Should().Be("""{"default":"Pear (orchard)"}""");
    }

    // A copy whose source cannot be read (here, missing) has no rank columns, so its codebook lists no choices either:
    // never its leftover inline ones.
    [Fact]
    public void CompilePersisted_RankingCopyOfAMissingQuestion_HasNoColumnsAndNoChoices()
    {
        // Arrange
        var definition = TopLevel(Ranking(""" "choicesFromQuestion":"gone","choices":["item1","item2"] """));

        // Act
        var columns = Flatten(definition);
        var codebook = Codebook(definition);

        // Assert
        RankingKeys(columns).Should().BeEmpty();
        Entries(codebook.GetProperty("questions").GetProperty("rank")).Should().BeEmpty();
        Entries(codebook.GetProperty("choiceCatalogs").GetProperty("rank")).Should().BeEmpty();
    }

    // As for a ranking with its own choices, Other is listed in the codebook after the copied choices but gets no column.
    [Fact]
    public void CompilePersisted_RankingCopyShowingOther_ListsOtherAfterTheCopiedChoices()
    {
        // Arrange
        var definition = TopLevel(Ranking(""" "choicesFromQuestion":"brands","showOtherItem":true """));

        // Act
        var columns = Flatten(definition);
        var codebook = Codebook(definition);

        // Assert
        RankingKeys(columns).Should().Equal("rank__a", "rank__b", "rank__c");
        Entries(codebook.GetProperty("questions").GetProperty("rank")).Should().Equal(
            [.. _expectedChoices, """{"value":"other","id":4,"text":{"default":"Other"}}"""]);
    }

    // The limit counts every choice the copy offers, its sources together, and names the ranking.
    [Fact]
    public void Flatten_RankingCopyOfferingMoreThanTheChoiceLimit_Throws()
    {
        // Arrange
        using var document = JsonDocument.Parse("""
            {"pages":[{"name":"p1","elements":[
              {"type":"checkbox","name":"fruit","choices":["apple","pear"]},
              {"type":"checkbox","name":"nuts","choices":["pear","pecan"]},
              {"type":"ranking","name":"rank","edxCarryForwardEnabled":true,"edxCarryForwardSources":["fruit","nuts"]}]}]}
            """);
        SchemaCompilationLimits limits = new() { MaxChoicesPerQuestion = 2 };

        // Act
        Action act = () => FormDefinitionFlattener.Flatten(document.RootElement, limits);

        // Assert
        var exception = act.Should().Throw<SchemaCompilationLimitExceededException>().Which;
        exception.LimitKind.Should().Be(SchemaCompilationLimitKind.MaxChoicesPerQuestion);
        exception.Context.Should().Be("rank");
    }

    // Stored before this fix, the ranking's codebook entry had no choices; a merge compile must fill them.
    [Fact]
    public void CompilePersisted_MergeOverACodebookWithoutTheCopiedChoices_ListsThem()
    {
        // Arrange
        var definition = TopLevel(Ranking(""" "choicesFromQuestion":"brands" """));
        FormSchemaCompiler compiler = new();
        var first = compiler.CompilePersisted(definition);
        var stale = WithoutRankChoices(first.CodebookJson);

        // Act
        var second = compiler.CompilePersisted(definition, first.FlatteningMapJson, stale, FormSchemaCompileMode.Merge);

        // Assert
        using var codebook = JsonDocument.Parse(second.CodebookJson);
        Entries(codebook.RootElement.GetProperty("questions").GetProperty("rank")).Should().Equal(_expectedChoices);
        Entries(codebook.RootElement.GetProperty("choiceCatalogs").GetProperty("rank")).Should().Equal(_expectedChoices);
    }

    private static readonly string[] _expectedChoices =
    [
        """{"value":"a","id":1,"text":{"default":"Apple","es":"Manzana"}}""",
        """{"value":"b","id":2,"text":{"default":"Pear"}}""",
        """{"value":"c","id":3,"text":{"default":"c"}}""",
    ];

    private static string Ranking(string copy) => $$"""{"type":"ranking","name":"rank",{{copy}}}""";

    private static string TopLevel(string questions) =>
        $$"""{"pages":[{"name":"p1","elements":[{{SOURCE}},{{questions}}]}]}""";

    private static string InLoop(string template) =>
        $$"""
        {"pages":[{"name":"p1","elements":[{{SOURCE}},
          {"type":"checkbox","name":"driver","choices":["x","y"]},
          {"type":"paneldynamic","name":"loop","loopSource":["driver"],"templateElements":[{{template}}]}]}]}
        """;

    private static IReadOnlyList<FormSchemaColumn> Flatten(string definitionJson)
    {
        using var definition = JsonDocument.Parse(definitionJson);
        return FormDefinitionFlattener.Flatten(definition.RootElement);
    }

    private static List<string> RankingKeys(IEnumerable<FormSchemaColumn> columns) =>
        columns.Where(column => column.Kind == FormSchemaColumnKind.RankingChoice).Select(column => column.Key).ToList();

    private static FormSchemaColumn RankingColumn(IEnumerable<FormSchemaColumn> columns, string key) =>
        columns.Single(column => column.Key == key);

    private static JsonElement Codebook(string definitionJson)
    {
        using var codebook = JsonDocument.Parse(new FormSchemaCompiler().CompilePersisted(definitionJson).CodebookJson);
        return codebook.RootElement.Clone();
    }

    private static List<string> Entries(JsonElement owner) =>
        owner.GetProperty("choices").EnumerateArray().Select(choice => choice.GetRawText()).ToList();

    private static string ChoiceLabel(JsonElement codebook, string columnKey) =>
        codebook.GetProperty("columns").GetProperty(columnKey).GetProperty("choiceLabel").GetRawText();

    private static string WithoutRankChoices(string codebookJson)
    {
        var codebook = System.Text.Json.Nodes.JsonNode.Parse(codebookJson)!;
        codebook["questions"]!["rank"]!["choices"] = new System.Text.Json.Nodes.JsonArray();
        codebook["choiceCatalogs"]!["rank"]!["choices"] = new System.Text.Json.Nodes.JsonArray();
        return codebook.ToJsonString();
    }
}
