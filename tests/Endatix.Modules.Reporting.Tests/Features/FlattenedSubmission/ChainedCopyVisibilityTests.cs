using Endatix.Modules.Reporting.Features.FormSchema.Choices;
using Endatix.Modules.Reporting.Features.FormSchema.FormSchema;
using static Endatix.Modules.Reporting.Tests.Features.FlattenedSubmission.CopiedChoiceVisibilityTests;

namespace Endatix.Modules.Reporting.Tests.Features.FlattenedSubmission;

/// <summary>
/// A copy whose source is itself a copy (#1754): <c>pick</c> copies <c>aware</c>, which copies <c>brands</c>. In
/// selected mode the copy shows what its direct source picked; in all or unselected mode it shows what its source
/// showed, so every link must be exact.
/// </summary>
public sealed class ChainedCopyVisibilityTests
{
    private const string BRANDS = """{"type":"checkbox","name":"brands","choices":["1","2","3"]}""";

    private const string CAPPED = """{"edxCarryForwardEnabled":true,"edxCarryForwardSources":["brands"],"edxCarryForwardMaxChoices":2}""";

    [Fact]
    public void Flatten_SelectedCopyOfASelectedCopy_ShowsWhatTheDirectSourcePicked()
    {
        // Arrange
        var definition = Survey(
            BRANDS,
            Aware("""{"choicesFromQuestion":"brands","choicesFromQuestionMode":"selected"}"""),
            Copy("checkbox", """{"choicesFromQuestion":"aware","choicesFromQuestionMode":"selected","showNoneItem":true}"""));

        // Act
        var cells = FlattenCopy(definition, """{"brands":["1","2","3"],"aware":["1","3"],"pick":["3"]}""", complete: false);

        // Assert
        cells.Should().Equal(Cells(("1", "0"), ("2", null), ("3", "1"), ("none", "0")));
    }

    [Fact]
    public void Flatten_SelectedCopyOfASelectedCopyInALoop_ShowsWhatTheDirectSourcePicked()
    {
        // Arrange
        var copy = Copy("checkbox", """{"choicesFromQuestion":"aware","choicesFromQuestionMode":"selected","showNoneItem":true}""");
        var definition = Survey(
            BRANDS,
            Aware("""{"choicesFromQuestion":"brands","choicesFromQuestionMode":"selected"}"""),
            $$"""{"type":"paneldynamic","name":"brandLoop","loopSource":["brands"],"templateElements":[{{copy}}]}""");

        // Act
        var flattened = Flatten(
            definition,
            """{"brands":["1","2","3"],"aware":["1","3"],"brandLoop":[{"itemValue":"2","loopIndex":0,"pick":["3"]},{"itemValue":"1","loopIndex":1,"pick":["1"]}]}""");

        // Assert
        Cell(flattened, "brandLoop__2__pick__1").Should().Be("0");
        Cell(flattened, "brandLoop__2__pick__2").Should().BeNull();
        Cell(flattened, "brandLoop__2__pick__3").Should().Be("1");
        Cell(flattened, "brandLoop__2__pick__none").Should().Be("0");
        Cell(flattened, "brandLoop__1__pick__1").Should().Be("1");
        Cell(flattened, "brandLoop__1__pick__3").Should().Be("0");
        Cell(flattened, "brandLoop__3__pick__1").Should().BeNull();
    }

    [Fact]
    public void Flatten_AllCopyOfAnAllCopy_ShowsEveryChoiceOfTheChain()
    {
        // Arrange
        var definition = Survey(
            BRANDS,
            Aware("""{"choicesFromQuestion":"brands"}"""),
            Copy("checkbox", """{"choicesFromQuestion":"aware","choicesFromQuestionMode":"all"}"""));

        // Act
        var cells = FlattenCopy(definition, """{"brands":["2"],"aware":["1"],"pick":["3"]}""", complete: false);

        // Assert
        cells.Should().Equal(Cells(("1", "0"), ("2", "0"), ("3", "1")));
    }

    [Fact]
    public void Flatten_UnselectedCopyOfASelectedCopy_ShowsWhatTheSourceShowedAndLeft()
    {
        // Arrange: aware showed 1 and 3 (picked in brands) and its respondent picked 1.
        var definition = Survey(
            BRANDS,
            Aware("""{"choicesFromQuestion":"brands","choicesFromQuestionMode":"selected"}"""),
            Copy("checkbox", """{"choicesFromQuestion":"aware","choicesFromQuestionMode":"unselected"}"""));

        // Act
        var cells = FlattenCopy(definition, """{"brands":["1","3"],"aware":["1"],"pick":["3"]}""", complete: false);

        // Assert
        cells.Should().Equal(Cells(("1", null), ("2", null), ("3", "1")));
    }

    [Fact]
    public void Flatten_UnselectedCopyOfAnAllCopy_LeavesWhatTheSourcePickedEmpty()
    {
        // Arrange
        var definition = Survey(
            BRANDS,
            Aware("""{"choicesFromQuestion":"brands"}"""),
            Copy("checkbox", """{"choicesFromQuestion":"aware","choicesFromQuestionMode":"unselected"}"""));

        // Act
        var cells = FlattenCopy(definition, """{"brands":["2"],"aware":["1"],"pick":["3"]}""", complete: false);

        // Assert
        cells.Should().Equal(Cells(("1", null), ("2", "0"), ("3", "1")));
    }

    [Theory]
    [InlineData("""{"choicesFromQuestion":"brands","choicesVisibleIf":"{item} != '2'"}""")]
    [InlineData(CAPPED)]
    public void Flatten_AllCopyOfAnInexactCopy_LeavesTheUnpickedChoicesEmpty(string awareProperties)
    {
        // Arrange
        var definition = Survey(
            BRANDS,
            Aware(awareProperties),
            Copy("checkbox", """{"choicesFromQuestion":"aware","choicesFromQuestionMode":"all","showNoneItem":true}"""));

        // Act
        var cells = FlattenCopy(definition, """{"brands":["2"],"aware":["1"],"pick":["3"]}""", complete: false);

        // Assert
        cells.Should().Equal(Cells(("1", null), ("2", null), ("3", "1"), ("none", null)));
    }

    [Fact]
    public void Flatten_SelectedCopyOfAnInexactCopy_ShowsWhatTheDirectSourcePicked()
    {
        // Arrange: a cap on aware shortens the list it showed, but what it picked was on that list.
        var definition = Survey(
            BRANDS,
            Aware(CAPPED),
            Copy("checkbox", """{"choicesFromQuestion":"aware","choicesFromQuestionMode":"selected"}"""));

        // Act
        var cells = FlattenCopy(definition, """{"brands":["1","2","3"],"aware":["1","3"],"pick":["3"]}""", complete: false);

        // Assert
        cells.Should().Equal(Cells(("1", "0"), ("2", null), ("3", "1")));
    }

    [Fact]
    public void Flatten_SelectedCopyOfAThreeLinkChain_ShowsWhatTheDirectSourcePicked()
    {
        // Arrange
        var definition = Survey(
            BRANDS,
            Aware("""{"choicesFromQuestion":"brands","choicesFromQuestionMode":"selected"}"""),
            """{"type":"checkbox","name":"tried","choicesFromQuestion":"aware","choicesFromQuestionMode":"selected"}""",
            Copy("checkbox", """{"choicesFromQuestion":"tried","choicesFromQuestionMode":"selected"}"""));

        // Act
        var cells = FlattenCopy(
            definition,
            """{"brands":["1","2","3"],"aware":["1","2","3"],"tried":["2","3"],"pick":["2"]}""",
            complete: false);

        // Assert
        cells.Should().Equal(Cells(("1", null), ("2", "1"), ("3", "0")));
    }

    [Fact]
    public void Flatten_AllCopyOfAnAllCopyOfASelectedCopy_FollowsTheWholeChain()
    {
        // Arrange: aware showed what brands picked (1, 3); tried and pick show all of that.
        var definition = Survey(
            BRANDS,
            Aware("""{"choicesFromQuestion":"brands","choicesFromQuestionMode":"selected"}"""),
            """{"type":"checkbox","name":"tried","choicesFromQuestion":"aware"}""",
            Copy("checkbox", """{"choicesFromQuestion":"tried"}"""));

        // Act
        var cells = FlattenCopy(
            definition,
            """{"brands":["1","3"],"aware":["3"],"tried":["1"],"pick":["3"]}""",
            complete: false);

        // Assert
        cells.Should().Equal(Cells(("1", "0"), ("2", null), ("3", "1")));
    }

    public static TheoryData<string, string, string?> ChainReasons => new()
    {
        { CAPPED, "all", nameof(UnknownBecause.SourceCopyIsNotExact) },
        { CAPPED, "unselected", nameof(UnknownBecause.SourceCopyIsNotExact) },
        { CAPPED, "selected", null },
        { """{"choicesFromQuestion":"brands"}""", "all", null },
    };

    [Theory]
    [MemberData(nameof(ChainReasons))]
    public void WhyUnknown_CopyOfACopy_NeedsAnExactSourceOnlyOutsideSelectedMode(string awareProperties, string mode, string? reason)
    {
        // Arrange
        var questions = Questions(
            BRANDS,
            Aware(awareProperties),
            Copy("checkbox", $$"""{"choicesFromQuestion":"aware","choicesFromQuestionMode":"{{mode}}"}"""));

        // Act
        var why = CopyVisibility.WhyUnknown(questions["pick"], questions, CopyDefinition.None);

        // Assert
        (why?.ToString()).Should().Be(reason);
    }

    [Fact]
    public void WhyUnknown_SelectedCopyOfACopyWithChoiceVisibilityRules_IsSourceChoicesVisibleIf()
    {
        // Arrange
        var questions = Questions(
            BRANDS,
            Aware("""{"choicesFromQuestion":"brands","choicesVisibleIf":"{item} != '2'"}"""),
            Copy("checkbox", """{"choicesFromQuestion":"aware","choicesFromQuestionMode":"selected"}"""));

        // Act
        var why = CopyVisibility.WhyUnknown(questions["pick"], questions, CopyDefinition.None);

        // Assert
        why.Should().Be(UnknownBecause.SourceChoicesVisibleIf);
    }

    [Theory]
    [MemberData(nameof(CopyTypes), MemberType = typeof(CopiedChoiceVisibilityTests))]
    public void Flatten_AllCopyOfAMultiSelectCopy_FollowsTheSourcesRule(string sourceType)
    {
        // Arrange
        var definition = Survey(
            BRANDS,
            $$"""{"type":"{{sourceType}}","name":"aware","choicesFromQuestion":"brands","choicesFromQuestionMode":"selected"}""",
            Copy("checkbox", """{"choicesFromQuestion":"aware"}"""));

        // Act
        var cells = FlattenCopy(definition, """{"brands":["1","3"],"aware":["3"],"pick":["1"]}""", complete: false);

        // Assert
        cells.Should().Equal(Cells(("1", "1"), ("2", null), ("3", "0")));
    }

    [Fact]
    public void Flatten_CopyOfACopyAndAnInlineSource_ShowsWhatEitherShowed()
    {
        // Arrange
        var definition = Survey(
            BRANDS,
            Aware("""{"choicesFromQuestion":"brands","choicesFromQuestionMode":"selected"}"""),
            """{"type":"checkbox","name":"online","choices":["3","4"]}""",
            Copy("checkbox", """{"edxCarryForwardEnabled":true,"edxCarryForwardSources":["aware","online"],"edxCarryForwardMode":"all"}"""));

        // Act
        var cells = FlattenCopy(definition, """{"brands":["1"],"aware":["1"],"online":["4"],"pick":["4"]}""", complete: false);

        // Assert
        cells.Should().Equal(Cells(("1", "0"), ("2", null), ("3", "0"), ("4", "1")));
    }

    // brands sits on a page with a visibleIf; the survey clears hidden answers on complete, so aware's rule, and the
    // rule of pick that reads it, hold only for a completed submission.
    [Theory]
    [InlineData(true, "0")]
    [InlineData(false, null)]
    public void Flatten_AllCopyOfACopyWhoseSourceIsUnderAVisibleIf_IsExactOnlyForACompleteSubmission(bool complete, string? unpicked)
    {
        // Arrange
        var definition = $$"""
            {"pages":[
              {"name":"p0","elements":[{"type":"text","name":"x"}]},
              {"name":"p1","visibleIf":"{x} = 1","elements":[{{BRANDS}}]},
              {"name":"p2","elements":[{{Aware("""{"choicesFromQuestion":"brands"}""")}},{{Copy("checkbox", """{"choicesFromQuestion":"aware","showNoneItem":true}""")}}]}]}
            """;

        // Act
        var cells = FlattenCopy(definition, """{"x":1,"brands":["2"],"aware":["1"],"pick":["3"]}""", complete);

        // Assert
        cells.Should().Equal(Cells(("1", unpicked), ("2", unpicked), ("3", "1"), ("none", unpicked)));
    }

    [Fact]
    public void Compile_AllCopyOfACopyWhoseSourceIsUnderAVisibleIf_RecordsWhenComplete()
    {
        // Arrange
        var definition = Survey(
            """{"type":"checkbox","name":"brands","visibleIf":"{x} = 1","choices":["1","2","3"]}""",
            Aware("""{"choicesFromQuestion":"brands"}"""),
            Copy("checkbox", """{"choicesFromQuestion":"aware"}"""));

        // Act
        var compiled = new FormSchemaCompiler().CompilePersisted(definition);

        // Assert
        Shown(compiled.CodebookJson).Should().Contain("\"whenComplete\":true");
    }

    [Fact]
    public void Flatten_MergeOverACodebookWithoutTheChainRule_LeavesTheUnpickedChoicesEmpty()
    {
        // Arrange: a 0.9.2 codebook marked the copy of a copy copied but stored no shown rule for it.
        var definition = Survey(
            BRANDS,
            Aware("""{"choicesFromQuestion":"brands","choicesFromQuestionMode":"selected"}"""),
            Copy("checkbox", """{"choicesFromQuestion":"aware","choicesFromQuestionMode":"selected","showNoneItem":true}"""));
        var current = new FormSchemaCompiler().CompilePersisted(definition);
        var codebook = System.Text.Json.Nodes.JsonNode.Parse(current.CodebookJson)!;
        codebook["choiceCatalogs"]!["pick"]!.AsObject().Remove("shown");
        var earlier = new FormSchemaCompiler().CompilePersisted(
            definition,
            current.FlatteningMapJson,
            codebook.ToJsonString(),
            FormSchemaCompileMode.Merge);

        // Act
        var merged = Recompile(earlier, definition, FormSchemaCompileMode.Merge);
        var cells = FlattenWith(merged, """{"brands":["1","2","3"],"aware":["1","3"],"pick":["3"]}""");

        // Assert
        Shown(merged.CodebookJson).Should().BeNull();
        cells.Should().Equal(Cells(("1", null), ("2", null), ("3", "1"), ("none", null)));
    }

    [Fact]
    public void Flatten_MergeAfterTheMiddleLinkChangedItsMode_LeavesTheUnpickedChoicesEmpty()
    {
        // Arrange: pick's own rule is the same in both versions; aware's changed from all to selected.
        var copy = Copy("checkbox", """{"choicesFromQuestion":"aware"}""");
        var first = new FormSchemaCompiler().CompilePersisted(
            Survey(BRANDS, Aware("""{"choicesFromQuestion":"brands"}"""), copy));

        // Act
        var merged = Recompile(
            first,
            Survey(BRANDS, Aware("""{"choicesFromQuestion":"brands","choicesFromQuestionMode":"selected"}"""), copy),
            FormSchemaCompileMode.Merge);
        var cells = FlattenWith(merged, """{"brands":["1","2"],"aware":["1"],"pick":["3"]}""");

        // Assert
        Shown(merged.CodebookJson).Should().NotBeNull();
        cells.Should().Equal(Cells(("1", null), ("2", null), ("3", "1")));
    }

    // Each level copies both questions of the level before in all mode, so a choice reaches pick along 2^24 paths.
    [Fact(Timeout = 10_000)]
    public async Task Flatten_DeepChainOfPairedCopies_ReadsEachSourceRuleOnce()
    {
        // Arrange
        const int levels = 24;
        List<string> questions =
        [
            """{"type":"checkbox","name":"q0","choices":["a","b"]}""",
            """{"type":"checkbox","name":"l0a","choicesFromQuestion":"q0","choicesFromQuestionMode":"selected"}""",
            """{"type":"checkbox","name":"l0b","choicesFromQuestion":"q0","choicesFromQuestionMode":"selected"}""",
        ];
        for (var level = 1; level <= levels; level++)
        {
            foreach (var side in new[] { "a", "b" })
            {
                questions.Add($$"""{"type":"checkbox","name":"l{{level}}{{side}}","edxCarryForwardEnabled":true,"edxCarryForwardSources":["l{{level - 1}}a","l{{level - 1}}b"],"edxCarryForwardMode":"all"}""");
            }
        }

        questions.Add(Copy("checkbox", $$"""{"edxCarryForwardEnabled":true,"edxCarryForwardSources":["l{{levels}}a","l{{levels}}b"],"edxCarryForwardMode":"all"}"""));
        var answers = "{" + string.Join(',', questions.Select(question => $$"""
            "{{Json(question).GetProperty("name").GetString()}}":["a"]
            """.Trim())) + "}";

        // Act
        var cells = await Task.Run(() => FlattenCopy(Survey([.. questions]), answers, complete: false), TestContext.Current.CancellationToken);

        // Assert
        cells.Should().Equal(Cells(("a", "1"), ("b", null)));
    }

    [Fact]
    public void Shows_StoredRulesThatCopyEachOther_IsUnknownInsteadOfLooping()
    {
        // Arrange: a codebook no compile writes, two copies whose rules name each other.
        var codebook = """
            {"choiceCatalogs":{
              "a":{"multiple":true,"copied":true,"choices":[{"value":"1","id":1}],"shown":{"mode":"all","sources":[{"name":"b","values":["1"]}]}},
              "b":{"multiple":true,"copied":true,"choices":[{"value":"1","id":1}],"shown":{"mode":"all","sources":[{"name":"a","values":["1"]}]}}}}
            """;
        var catalogs = ChoiceCatalogs.Read(codebook);
        var scope = AnswerScope.Root(Json("""{"a":["1"],"b":["1"]}"""));

        // Act
        var shown = catalogs.VisibilityOf("a")!.Shows("2", scope, catalogs);

        // Assert
        shown.Should().BeNull();
    }

    [Fact]
    public void WhyUnknown_AllCopyOfASingleSelectCopy_IsSourceCopyIsNotExact()
    {
        // Arrange: only a checkbox or tagbox stores its shown rule, so a radiogroup's list cannot be read back.
        var questions = Questions(
            BRANDS,
            """{"type":"radiogroup","name":"aware","choicesFromQuestion":"brands"}""",
            Copy("checkbox", """{"choicesFromQuestion":"aware"}"""));

        // Act
        var why = CopyVisibility.WhyUnknown(questions["pick"], questions, CopyDefinition.None);

        // Assert
        why.Should().Be(UnknownBecause.SourceCopyIsNotExact);
    }

    [Fact]
    public void WhyUnknown_CopyOfACycle_IsSourceCopyIsNotExact()
    {
        // Arrange
        var questions = Questions(
            """{"type":"checkbox","name":"aware","choicesFromQuestion":"tried"}""",
            """{"type":"checkbox","name":"tried","choicesFromQuestion":"aware"}""",
            Copy("checkbox", """{"choicesFromQuestion":"aware"}"""));

        // Act
        var why = CopyVisibility.WhyUnknown(questions["pick"], questions, CopyDefinition.None);

        // Assert
        why.Should().Be(UnknownBecause.SourceCopyIsNotExact);
    }

    private static string Aware(string properties) => $$"""{"type":"checkbox","name":"aware",{{properties[1..]}}""";

    private static Dictionary<string, System.Text.Json.JsonElement> Questions(params string[] questions) =>
        questions.Select(Json).ToDictionary(question => question.GetProperty("name").GetString()!, StringComparer.Ordinal);
}
