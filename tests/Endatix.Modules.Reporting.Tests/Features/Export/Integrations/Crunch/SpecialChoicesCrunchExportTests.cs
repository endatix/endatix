using System.Text.Json;
using System.Text.Json.Nodes;
using Endatix.Modules.Reporting.Tests.Features.FormSchema.FormSchema;
using Endatix.Modules.Reporting.Tests.Features.FormSchema.Personalization;
using FluentAssertions;

namespace Endatix.Modules.Reporting.Tests.Features.Export.Integrations.Crunch;

/// <summary>
/// None, Refused and Don't know in the two files a Crunch import takes: the categories, which of them are missing, and
/// category ids that stay put when the form changes or a dataset is exported again.
/// </summary>
public sealed class SpecialChoicesCrunchExportTests
{
    private const string SHOJI_SETTINGS = """{"aliasProfile":"native","keySeparator":"--"}""";

    [Fact]
    public async Task ShojiCodebook_SpecialItems_AreCategoriesAndRefusedAndDontKnowAreMissing()
    {
        // Arrange
        const string definition = """
            {"pages":[{"name":"p1","elements":[{"type":"radiogroup","name":"rg","choices":["a","b"],
              "showOtherItem":true,"showNoneItem":true,"showRefuseItem":true,"showDontKnowItem":true}]}]}
            """;
        var pipeline = PersonalizationPipeline.Compile(definition);

        // Act
        var shoji = await CrunchExport.ShojiCodebookAsync(pipeline.Entity(), TestContext.Current.CancellationToken, SHOJI_SETTINGS);

        // Assert
        Categories(shoji, "rg").Should().Equal(
            (1, "a", false),
            (2, "b", false),
            (3, "Other", false),
            (4, "None", false),
            (5, "Refuse to answer", true),
            (6, "Don't know", true));
    }

    [Fact]
    public async Task CsvShoji_SingleSelectSpecialItem_WritesItsCategoryId()
    {
        // Arrange
        const string definition = """
            {"pages":[{"name":"p1","elements":[{"type":"dropdown","name":"dd","choices":["a"],"showNoneItem":true,"showDontKnowItem":true},
              {"type":"checkbox","name":"cb","choices":["x"],"showRefuseItem":true}]}]}
            """;
        IReadOnlyList<FixtureResponse> responses =
        [
            new("none", """{"dd":"none","cb":["refused"]}""", null),
            new("dontknow", """{"dd":"dontknow","cb":["x"]}""", null),
        ];
        var pipeline = PersonalizationPipeline.Compile(definition);
        pipeline.FlattenAll(definition, responses);
        var cancellationToken = TestContext.Current.CancellationToken;

        // Act
        var shoji = await CrunchExport.ShojiCodebookAsync(pipeline.Entity(), cancellationToken, SHOJI_SETTINGS);
        var csv = await CrunchExport.CsvAsync(new(pipeline.Entity(), pipeline.ExportRows(responses), SHOJI_SETTINGS), cancellationToken);

        // Assert
        CrunchImportCheck.Problems(shoji, csv).Should().BeEmpty();
        Column(csv, "dd").Should().Equal("2", "3");
        Column(csv, "cb--refused").Should().Equal("1", "0");
    }

    // SurveyJS demo "Exclusive choice options for multiple-choice question": a custom isExclusive choice is still an
    // inline choice; only showRefuseItem makes a special item. Exclusivity alone never makes a choice special.
    [Fact]
    public async Task Export_CustomExclusiveChoiceWithRefused_CustomStaysInlineAndRefusedIsSpecial()
    {
        // Arrange
        const string definition = """
            {"pages":[{"name":"p1","elements":[{"type":"checkbox","name":"work","choices":["office","travel",
              {"value":"home","text":"I work from home","isExclusive":true}],"showRefuseItem":true},
              {"type":"radiogroup","name":"where","choices":["office",{"value":"home","isExclusive":true}],"showRefuseItem":true}]}]}
            """;
        IReadOnlyList<FixtureResponse> responses =
        [
            new("home", """{"work":["home"],"where":"home"}""", null),
            new("refused", """{"work":["refused"],"where":"refused"}""", null),
        ];
        var pipeline = PersonalizationPipeline.Compile(definition);
        pipeline.FlattenAll(definition, responses);
        var cancellationToken = TestContext.Current.CancellationToken;

        // Act
        var shoji = await CrunchExport.ShojiCodebookAsync(pipeline.Entity(), cancellationToken, SHOJI_SETTINGS);
        var csv = await CrunchExport.CsvAsync(new(pipeline.Entity(), pipeline.ExportRows(responses), SHOJI_SETTINGS), cancellationToken);

        // Assert: the catalog marks home exclusive without a kind, refused with its kind.
        using var codebook = JsonDocument.Parse(pipeline.Entity().Codebook);
        codebook.RootElement.GetProperty("choiceCatalogs").GetProperty("work").GetProperty("choices").EnumerateArray()
            .Select(choice => choice.GetRawText()).Should().Equal(
                """{"value":"office","id":1,"text":{"default":"office"}}""",
                """{"value":"travel","id":2,"text":{"default":"travel"}}""",
                """{"value":"home","id":3,"exclusive":true,"text":{"default":"I work from home"}}""",
                """{"value":"refused","id":4,"kind":"refused","exclusive":true,"text":{"default":"Refuse to answer"}}""");
        Column(csv, "work--home").Should().Equal("1", "0");
        Column(csv, "work--refused").Should().Equal("0", "1");
        Categories(shoji, "where").Should().Equal((1, "office", false), (2, "home", false), (3, "Refuse to answer", true));
        Column(csv, "where").Should().Equal("2", "3");
        CrunchImportCheck.Problems(shoji, csv).Should().BeEmpty();
    }

    [Fact]
    public async Task Export_AfterAChoiceIsRemovedAndNoneAdded_IdsComeFromTheMergedCatalog()
    {
        // Arrange: v1 stores a=1, b=2, c=3. v2 drops b and adds None; the stored ids must hold.
        const string v1 = """{"pages":[{"name":"p1","elements":[{"type":"radiogroup","name":"rg","choices":["a","b","c"]}]}]}""";
        const string v2 = """{"pages":[{"name":"p1","elements":[{"type":"radiogroup","name":"rg","choices":["a","c"],"showNoneItem":true}]}]}""";
        IReadOnlyList<FixtureResponse> responses =
        [
            new("old-b", """{"rg":"b"}""", null),
            new("c", """{"rg":"c"}""", null),
            new("none", """{"rg":"none"}""", null),
        ];
        var pipeline = PersonalizationPipeline.Compile(v1);
        pipeline.Recompile(v2, Endatix.Modules.Reporting.Features.FormSchema.FormSchema.FormSchemaCompileMode.Merge);
        pipeline.FlattenAll(v2, responses);
        var cancellationToken = TestContext.Current.CancellationToken;

        // Act
        var shoji = await CrunchExport.ShojiCodebookAsync(pipeline.Entity(), cancellationToken, SHOJI_SETTINGS);
        var csv = await CrunchExport.CsvAsync(new(pipeline.Entity(), pipeline.ExportRows(responses), SHOJI_SETTINGS), cancellationToken);

        // Assert
        Categories(shoji, "rg").Select(category => (category.Id, category.Name)).Should().Equal(
            (1, "a"), (2, "b"), (3, "c"), (4, "None"));
        Column(csv, "rg").Should().Equal("2", "3", "4");
        CrunchImportCheck.Problems(shoji, csv).Should().BeEmpty();
    }

    [Fact]
    public async Task Export_AfterAnInlineChoiceIsAddedBehindNone_NoneKeepsItsId()
    {
        // Arrange: v1 stores a=1, b=2, none=3. v2 adds "c", which the definition lists before None.
        const string v1 = """{"pages":[{"name":"p1","elements":[{"type":"radiogroup","name":"rg","choices":["a","b"],"showNoneItem":true}]}]}""";
        const string v2 = """{"pages":[{"name":"p1","elements":[{"type":"radiogroup","name":"rg","choices":["a","b","c"],"showNoneItem":true}]}]}""";
        IReadOnlyList<FixtureResponse> responses = [new("none", """{"rg":"none"}""", null), new("c", """{"rg":"c"}""", null)];
        var pipeline = PersonalizationPipeline.Compile(v1);
        pipeline.Recompile(v2, Endatix.Modules.Reporting.Features.FormSchema.FormSchema.FormSchemaCompileMode.Merge);
        pipeline.FlattenAll(v2, responses);
        var cancellationToken = TestContext.Current.CancellationToken;

        // Act
        var shoji = await CrunchExport.ShojiCodebookAsync(pipeline.Entity(), cancellationToken, SHOJI_SETTINGS);
        var csv = await CrunchExport.CsvAsync(new(pipeline.Entity(), pipeline.ExportRows(responses), SHOJI_SETTINGS), cancellationToken);

        // Assert
        Categories(shoji, "rg").Select(category => (category.Id, category.Name)).Should().Equal(
            (1, "a"), (2, "b"), (3, "None"), (4, "c"));
        Column(csv, "rg").Should().Equal("3", "4");
    }

    [Fact]
    public async Task ShojiCodebook_ValueRenamedWithTheSameLabel_KeepsCategoryNamesUnique()
    {
        // Arrange: v2 renames value "b" to "blue" and keeps the label; Crunch needs unique category names.
        const string v1 = """{"pages":[{"name":"p1","elements":[{"type":"radiogroup","name":"rg","choices":["a",{"value":"b","text":"Blue"}]}]}]}""";
        const string v2 = """{"pages":[{"name":"p1","elements":[{"type":"radiogroup","name":"rg","choices":["a",{"value":"blue","text":"Blue"}]}]}]}""";
        var pipeline = PersonalizationPipeline.Compile(v1);
        pipeline.Recompile(v2, Endatix.Modules.Reporting.Features.FormSchema.FormSchema.FormSchemaCompileMode.Merge);

        // Act
        var shoji = await CrunchExport.ShojiCodebookAsync(pipeline.Entity(), TestContext.Current.CancellationToken, SHOJI_SETTINGS);

        // Assert
        Categories(shoji, "rg").Select(category => (category.Id, category.Name)).Should().Equal(
            (1, "a"), (2, "Blue (b)"), (3, "Blue"));
    }

    [Fact]
    public async Task ReExport_IntoAnExistingDataset_KeepsEveryAliasAndCategoryId()
    {
        // Arrange: the first export is today's all-questions form; the form then shows special items, merges and is
        // backfilled, and is exported again into the same dataset.
        var v1 = FormSchemaFixtureLoader.LoadAllQuestionsText("all-questions-definition.json");
        var v2 = WithSpecialItems(v1);
        var answers = FormSchemaFixtureLoader.LoadAllQuestionsText("all-questions-submission.json");
        var cancellationToken = TestContext.Current.CancellationToken;
        IReadOnlyList<FixtureResponse> first = [new("r1", answers, null)];
        var pipeline = PersonalizationPipeline.Compile(v1);
        pipeline.FlattenAll(v1, first);
        var shojiBefore = await CrunchExport.ShojiCodebookAsync(pipeline.Entity(), cancellationToken, SHOJI_SETTINGS);

        IReadOnlyList<FixtureResponse> second =
        [
            .. first,
            new("r2", WithAnswers(answers, """{"qRadioGroup":"refused","qDropdown":"none","qTagBox":["none"]}"""), null),
        ];
        pipeline.Recompile(v2, Endatix.Modules.Reporting.Features.FormSchema.FormSchema.FormSchemaCompileMode.Merge);
        pipeline.FlattenAll(v2, second);

        // Act
        var shojiAfter = await CrunchExport.ShojiCodebookAsync(pipeline.Entity(), cancellationToken, SHOJI_SETTINGS);
        var csvAfter = await CrunchExport.CsvAsync(new(pipeline.Entity(), pipeline.ExportRows(second), SHOJI_SETTINGS), cancellationToken);

        // Assert
        var before = Variables(shojiBefore);
        var after = Variables(shojiAfter);
        foreach (var (alias, variable) in before)
        {
            after.Should().ContainKey(alias);
            after[alias].Categories.Should().StartWith(variable.Categories, $"categories of '{alias}' keep their ids");
            after[alias].Subvariables.Should().StartWith(variable.Subvariables, $"subvariables of '{alias}' keep their aliases");
        }

        Categories(shojiAfter, "qRadioGroup").Select(category => (category.Id, category.Name, category.Missing))
            .Should().EndWith([(4, "None", false), (5, "Refuse to answer", true)]);
        Column(csvAfter, "qRadioGroup")[1].Should().Be("5");
        Column(csvAfter, "qDropdown").Should().Equal("2", "4");
        Column(csvAfter, "qTagBox--none").Should().Equal("0", "1");
        CrunchImportCheck.Problems(shojiAfter, csvAfter).Should().BeEmpty();
    }

    private static string WithSpecialItems(string definition)
    {
        var root = JsonNode.Parse(definition)!;
        foreach (var question in Questions(root))
        {
            switch (question["name"]?.GetValue<string>())
            {
                case "qRadioGroup":
                    question["showNoneItem"] = true;
                    question["showRefuseItem"] = true;
                    break;
                case "qDropdown":
                    question["showNoneItem"] = true;
                    break;
                case "qTagBox":
                    question["showNoneItem"] = true;
                    question["showDontKnowItem"] = true;
                    break;
            }
        }

        return root.ToJsonString();
    }

    private static IEnumerable<JsonObject> Questions(JsonNode node)
    {
        foreach (var page in node["pages"]!.AsArray())
        {
            foreach (var question in Walk(page!["elements"]))
            {
                yield return question;
            }
        }
    }

    private static IEnumerable<JsonObject> Walk(JsonNode? elements)
    {
        foreach (var element in elements?.AsArray() ?? [])
        {
            var question = element!.AsObject();
            yield return question;
            foreach (var child in Walk(question["elements"]).Concat(Walk(question["templateElements"])))
            {
                yield return child;
            }
        }
    }

    private static string WithAnswers(string answers, string changes)
    {
        var root = JsonNode.Parse(answers)!.AsObject();
        foreach (var (name, value) in JsonNode.Parse(changes)!.AsObject())
        {
            root[name] = value?.DeepClone();
        }

        return root.ToJsonString();
    }

    private static List<(int Id, string Name, bool Missing)> Categories(string shoji, string alias)
    {
        using var document = JsonDocument.Parse(shoji);
        return Metadata(document)[alias].GetProperty("categories").EnumerateArray()
            .Select(category => (
                category.GetProperty("id").GetInt32(),
                category.GetProperty("name").GetString()!,
                category.GetProperty("missing").GetBoolean()))
            .ToList();
    }

    private static Dictionary<string, (List<string> Categories, List<string> Subvariables)> Variables(string shoji)
    {
        using var document = JsonDocument.Parse(shoji);
        return Metadata(document).ToDictionary(
            variable => variable.Key,
            variable => (
                variable.Value.TryGetProperty("categories", out var categories)
                    ? categories.EnumerateArray().Select(category => category.GetRawText()).ToList()
                    : [],
                variable.Value.TryGetProperty("subvariables", out var subvariables)
                    ? subvariables.EnumerateArray().Select(subvariable => subvariable.GetProperty("alias").GetString()!).ToList()
                    : []),
            StringComparer.Ordinal);
    }

    private static Dictionary<string, JsonElement> Metadata(JsonDocument document) =>
        document.RootElement.GetProperty("body").GetProperty("table").GetProperty("metadata").EnumerateObject()
            .ToDictionary(variable => variable.Name, variable => variable.Value.Clone(), StringComparer.Ordinal);

    private static List<string> Column(string csv, string header)
    {
        var (headers, rows) = CrunchImportCheck.Read(csv);
        var index = headers.IndexOf(header);
        index.Should().BeGreaterThanOrEqualTo(0, $"the CSV has a '{header}' column");
        return rows.Select(row => row[index]).ToList();
    }
}
