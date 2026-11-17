using System.Text.Json;
using Endatix.Modules.Reporting.Features.FormSchema.FlattenedFormDefinition;
using Endatix.Modules.Reporting.Features.FormSchema.FormSchema;

namespace Endatix.Modules.Reporting.Tests.Features.FormSchema.FlattenedFormDefinition;

/// <summary>
/// A checkbox or tagbox that copies its choices (<c>choicesFromQuestion</c> or Hub carry forward) compiles to one
/// indicator column per choice its sources offer, whatever the mode, then its own special items; its codebook lists
/// those choices with the source's labels.
/// </summary>
public sealed class CheckboxCopiedChoicesTests
{
    private const string SOURCE = """
        {"type":"checkbox","name":"brands","choices":[{"value":"a","text":{"default":"Apple","es":"Manzana"}},{"value":"b","text":"Pear"},"c"]}
        """;

    public static TheoryData<string, string> Copies
    {
        get
        {
            TheoryData<string, string> data = [];
            string[] copies =
            [
                """ "choicesFromQuestion":"brands","choicesFromQuestionMode":"all" """,
                """ "choicesFromQuestion":"brands","choicesFromQuestionMode":"selected" """,
                """ "choicesFromQuestion":"brands","choicesFromQuestionMode":"unselected" """,
                """ "edxCarryForwardEnabled":true,"edxCarryForwardSources":["brands"],"edxCarryForwardMode":"all" """,
                """ "edxCarryForwardEnabled":true,"edxCarryForwardSources":["brands"],"edxCarryForwardMode":"selected" """,
                """ "edxCarryForwardEnabled":true,"edxCarryForwardSources":["brands"],"edxCarryForwardMode":"unselected" """,
            ];
            foreach (var type in new[] { "checkbox", "tagbox" })
            {
                foreach (var copy in copies)
                {
                    data.Add(type, copy);
                }
            }

            return data;
        }
    }

    [Theory]
    [MemberData(nameof(Copies))]
    public void Flatten_TopLevelCopy_HasOneIndicatorPerSourceChoiceInEveryMode(string type, string copy)
    {
        // Act
        var columns = Flatten(TopLevel(Pick(type, copy)));

        // Assert
        IndicatorKeys(columns).Should().Equal("pick__a", "pick__b", "pick__c");
        columns.Should().NotContain(column => column.Key == "pick");
        columns.Single(column => column.Key == "pick__b").Label.Should().Be("pick — Pear");
    }

    [Theory]
    [MemberData(nameof(Copies))]
    public void Flatten_CopyInsideALoop_HasOneIndicatorPerIterationAndSourceChoiceInEveryMode(string type, string copy)
    {
        // Act
        var columns = Flatten(InLoop(Pick(type, copy)));

        // Assert
        IndicatorKeys(columns).Should().Equal(
            "loop__x__pick__a", "loop__x__pick__b", "loop__x__pick__c",
            "loop__y__pick__a", "loop__y__pick__b", "loop__y__pick__c");
    }

    [Theory]
    [MemberData(nameof(Copies))]
    public void CompilePersisted_TopLevelCopy_ListsTheSourceChoicesWithTheirLabels(string type, string copy)
    {
        // Act
        var codebook = Codebook(TopLevel(Pick(type, copy)));

        // Assert
        var question = codebook.GetProperty("questions").GetProperty("pick");
        question.GetProperty("exportShape").GetString().Should().Be("multiple_response");
        question.GetProperty("choiceSource").GetProperty("kind").GetString().Should().Be("carryForward");
        Entries(question).Should().Equal(_expectedChoices);
        Entries(codebook.GetProperty("choiceCatalogs").GetProperty("pick")).Should().Equal(_expectedChoices);
        ChoiceLabel(codebook, "pick__a").Should().Be("""{"default":"Apple","es":"Manzana"}""");
        ChoiceLabel(codebook, "pick__c").Should().Be("""{"default":"c"}""");
    }

    [Theory]
    [MemberData(nameof(Copies))]
    public void CompilePersisted_CopyInsideALoop_ListsTheSourceChoicesWithTheirLabels(string type, string copy)
    {
        // Act
        var codebook = Codebook(InLoop(Pick(type, copy)));

        // Assert
        Entries(codebook.GetProperty("questions").GetProperty("pick")).Should().Equal(_expectedChoices);
        ChoiceLabel(codebook, "loop__y__pick__b").Should().Be("""{"default":"Pear"}""");
    }

    // Creator leaves its default items on a question that copies its choices; the respondent never sees them.
    [Fact]
    public void Flatten_CopyWithLeftoverChoices_HasIndicatorsForTheCopiedChoicesOnly()
    {
        // Arrange
        var copy = Pick("checkbox", """ "choicesFromQuestion":"brands","choices":["item1","item2","item3"],"showNoneItem":true """);

        // Act
        var topLevel = Flatten(TopLevel(copy));
        var inLoop = Flatten(InLoop(copy));

        // Assert
        IndicatorKeys(topLevel).Should().Equal("pick__a", "pick__b", "pick__c", "pick__none");
        IndicatorKeys(inLoop).Should().Equal(
            "loop__x__pick__a", "loop__x__pick__b", "loop__x__pick__c", "loop__x__pick__none",
            "loop__y__pick__a", "loop__y__pick__b", "loop__y__pick__c", "loop__y__pick__none");
    }

    // The special items are the copy's own, after the copied choices; Other keeps its text column.
    [Fact]
    public void CompilePersisted_CopyShowingNoneAndOther_ListsThemAfterTheCopiedChoices()
    {
        // Arrange
        var definition = TopLevel(Pick("checkbox", """ "choicesFromQuestion":"brands","showNoneItem":true,"showOtherItem":true """));

        // Act
        var columns = Flatten(definition);
        var codebook = Codebook(definition);

        // Assert
        columns.Where(column => column.SourceQuestion == "pick").Select(column => column.Key).Should().Equal(
            "pick__a", "pick__b", "pick__c", "pick__other", "pick__other_text", "pick__none");
        Entries(codebook.GetProperty("questions").GetProperty("pick")).Should().Equal(
        [
            .. _expectedChoices,
            """{"value":"other","id":4,"kind":"other","text":{"default":"Other"}}""",
            """{"value":"none","id":5,"kind":"none","exclusive":true,"text":{"default":"None"}}""",
        ]);
        ChoiceLabel(codebook, "pick__none").Should().Be("""{"default":"None"}""");
    }

    // Hub publishes the choices of the question at the root of the chain, read with that question's labels.
    [Fact]
    public void CompilePersisted_CopyOfACopy_UsesTheRootQuestionsChoicesAndLabels()
    {
        // Arrange
        var definition = TopLevel("""
            {"type":"checkbox","name":"seen","choicesFromQuestion":"brands","choicesFromQuestionMode":"selected"},
            {"type":"tagbox","name":"pick","choicesFromQuestion":"seen","choicesFromQuestionMode":"selected"}
            """);

        // Act
        var columns = Flatten(definition);
        var codebook = Codebook(definition);

        // Assert
        IndicatorKeys(columns).Should().Equal("seen__a", "seen__b", "seen__c", "pick__a", "pick__b", "pick__c");
        Entries(codebook.GetProperty("questions").GetProperty("pick")).Should().Equal(_expectedChoices);
        ChoiceLabel(codebook, "pick__a").Should().Be("""{"default":"Apple","es":"Manzana"}""");
    }

    // Hub carry forward offers the union of its sources in source order, the first occurrence of a value winning.
    [Fact]
    public void CompilePersisted_CopyFromTwoSources_HasTheUnionInSourceOrder()
    {
        // Arrange
        var definition = TopLevel("""
            {"type":"radiogroup","name":"orchard","choices":[{"value":"b","text":"Pear (orchard)"},"z"]},
            {"type":"checkbox","name":"pick","edxCarryForwardEnabled":true,"edxCarryForwardSources":["orchard","brands"]}
            """);

        // Act
        var columns = Flatten(definition);
        var codebook = Codebook(definition);

        // Assert
        IndicatorKeys(columns).Where(key => key.StartsWith("pick__", StringComparison.Ordinal))
            .Should().Equal("pick__b", "pick__z", "pick__a", "pick__c");
        ChoiceLabel(codebook, "pick__b").Should().Be("""{"default":"Pear (orchard)"}""");
    }

    // A copy whose source cannot be read (here, missing) keeps one text column of the picked values, never its
    // leftover inline choices, and its codebook says it is degraded.
    [Fact]
    public void CompilePersisted_CopyOfAMissingQuestion_KeepsOneTextColumn()
    {
        // Arrange
        var definition = TopLevel(Pick("checkbox", """ "choicesFromQuestion":"gone","choices":["item1","item2"] """));

        // Act
        var columns = Flatten(definition);
        var codebook = Codebook(definition);

        // Assert
        IndicatorKeys(columns).Where(key => key.StartsWith("pick", StringComparison.Ordinal)).Should().BeEmpty();
        columns.Single(column => column.Key == "pick").Kind.Should().Be(FormSchemaColumnKind.Simple);
        var question = codebook.GetProperty("questions").GetProperty("pick");
        question.GetProperty("degraded").GetString().Should().Be("text");
        Entries(question).Should().BeEmpty();
    }

    // A tagbox fed from a data list has no choices on the form until #1160: it stays one text column.
    [Fact]
    public void Flatten_TagboxFromADataList_KeepsOneTextColumn()
    {
        // Act
        var columns = Flatten(TopLevel(Pick("tagbox", """ "edxDataListId":"list-1" """)));

        // Assert
        columns.Single(column => column.SourceQuestion == "pick").Kind.Should().Be(FormSchemaColumnKind.Simple);
    }

    // The limit counts every choice the copy offers, its sources together, and its special items.
    [Fact]
    public void Flatten_CopyOfferingMoreThanTheChoiceLimit_Throws()
    {
        // Arrange
        using var document = JsonDocument.Parse("""
            {"pages":[{"name":"p1","elements":[
              {"type":"radiogroup","name":"fruit","choices":["apple","pear"]},
              {"type":"checkbox","name":"pick","choicesFromQuestion":"fruit","showNoneItem":true}]}]}
            """);
        SchemaCompilationLimits limits = new() { MaxChoicesPerQuestion = 2 };

        // Act
        Action act = () => FormDefinitionFlattener.Flatten(document.RootElement, limits);

        // Assert
        var exception = act.Should().Throw<SchemaCompilationLimitExceededException>().Which;
        exception.LimitKind.Should().Be(SchemaCompilationLimitKind.MaxChoicesPerQuestion);
        exception.Context.Should().Be("pick");
    }

    private static readonly string[] _expectedChoices =
    [
        """{"value":"a","id":1,"text":{"default":"Apple","es":"Manzana"}}""",
        """{"value":"b","id":2,"text":{"default":"Pear"}}""",
        """{"value":"c","id":3,"text":{"default":"c"}}""",
    ];

    private static string Pick(string type, string copy) => $$"""{"type":"{{type}}","name":"pick",{{copy}}}""";

    private static string TopLevel(string questions) =>
        $$"""{"pages":[{"name":"p1","elements":[{{SOURCE}},{{questions}}]}]}""";

    private static string InLoop(string template) =>
        $$"""
        {"pages":[{"name":"p1","elements":[{{SOURCE}},
          {"type":"radiogroup","name":"driver","choices":["x","y"]},
          {"type":"paneldynamic","name":"loop","loopSource":["driver"],"templateElements":[{{template}}]}]}]}
        """;

    private static IReadOnlyList<FormSchemaColumn> Flatten(string definitionJson)
    {
        using var definition = JsonDocument.Parse(definitionJson);
        return FormDefinitionFlattener.Flatten(definition.RootElement);
    }

    // The source question's own indicators come first at top level; a loop's driver is a radiogroup with none.
    private static List<string> IndicatorKeys(IEnumerable<FormSchemaColumn> columns) =>
        columns.Where(column => column.Kind == FormSchemaColumnKind.ChoiceIndicator && column.SourceQuestion != "brands")
            .Select(column => column.Key).ToList();

    private static JsonElement Codebook(string definitionJson)
    {
        using var codebook = JsonDocument.Parse(new FormSchemaCompiler().CompilePersisted(definitionJson).CodebookJson);
        return codebook.RootElement.Clone();
    }

    private static List<string> Entries(JsonElement owner) =>
        owner.GetProperty("choices").EnumerateArray().Select(choice => choice.GetRawText()).ToList();

    private static string ChoiceLabel(JsonElement codebook, string columnKey) =>
        codebook.GetProperty("columns").GetProperty(columnKey).GetProperty("choiceLabel").GetRawText();
}
