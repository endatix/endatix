using System.Text.Json;
using Endatix.Modules.Reporting.Tests.Features.FormSchema.Personalization;
using FluentAssertions;

namespace Endatix.Modules.Reporting.Tests.Features.Export.Integrations.Crunch;

/// <summary>
/// A question loop (<c>loopSource</c>) whose source copies its choices from other questions, or is a ranking, in the
/// two files a Crunch import takes. The loop repeats once per choice the source can offer, and each stored panel item
/// is matched to its iteration by its <c>itemValue</c>, whatever its position in the array.
/// </summary>
/// <remarks>
/// An iteration is labelled with its choice's text from the source's codebook entry. A ranking (#1723) or a checkbox
/// (#1724) that copies its choices publishes them with the source's labels.
/// </remarks>
public sealed class LoopOverCopiedChoicesExportTests
{
    private const string SHOJI_SETTINGS = """{"aliasProfile":"native","keySeparator":"--"}""";

    private const string LOOPED_QUESTION = """
        {"type":"radiogroup","name":"q","choices":[{"value":"yes","text":"Yes"},{"value":"no","text":"No"}]}
        """;

    // #1555 broke this in v0.9.1: up to v0.9.0 a ranking with its own choices drove a loop.
    [Fact]
    public async Task Export_LoopOverARankingWithItsOwnChoices_ExportsOneIterationPerChoice()
    {
        // Arrange
        var definition = Definition("""
            {"type":"ranking","name":"rank","choices":[{"value":"a","text":"Alpha"},{"value":"b","text":"Beta"}]}
            """, "rank");
        IReadOnlyList<FixtureResponse> responses =
        [
            new("r1", """{"rank":["b","a"],"loop":[{"itemValue":"b","loopIndex":0,"q":"no"},{"itemValue":"a","loopIndex":1,"q":"yes"}]}""", null),
        ];

        // Act
        var (shoji, csv) = await ExportAsync(definition, responses);

        // Assert
        Subvariables(shoji, "loop--q").Should().Equal(("loop--a--q", "Alpha"), ("loop--b--q", "Beta"));
        CrunchImportCheck.Problems(shoji, csv).Should().BeEmpty();
        Column(csv, "loop--a--q").Should().Equal("1");
        Column(csv, "loop--b--q").Should().Equal("2");
    }

    // The source copies from a question that copies too, as a brand list shown once and then narrowed down does.
    // Panel items are stored in the order the respondent saw them, and an unpicked choice has no item.
    [Fact]
    public async Task Export_LoopOverAChainedCopy_ExportsOneIterationPerSourceChoiceKeyedByItemValue()
    {
        // Arrange
        var definition = Definition("""
            {"type":"checkbox","name":"brands","choices":[{"value":"1","text":"Brand 1"},{"value":"2","text":"Brand 2"},{"value":"3","text":"Brand 3"}]},
            {"type":"checkbox","name":"seen","choicesFromQuestion":"brands","choicesFromQuestionMode":"selected"},
            {"type":"checkbox","name":"aware","choicesFromQuestion":"seen","choicesFromQuestionMode":"selected"}
            """, "aware");
        IReadOnlyList<FixtureResponse> responses =
        [
            new("r1", """
                {"brands":["1","2","3"],"seen":["1","3"],"aware":["3","1"],
                 "loop":[{"itemValue":"3","loopIndex":1,"q":"no"},{"itemValue":"1","loopIndex":0,"q":"yes"}]}
                """, null),
        ];

        // Act
        var (shoji, csv) = await ExportAsync(definition, responses);

        // Assert
        Subvariables(shoji, "loop--q").Should().Equal(
            ("loop--1--q", "Brand 1"), ("loop--2--q", "Brand 2"), ("loop--3--q", "Brand 3"));
        CrunchImportCheck.Problems(shoji, csv).Should().BeEmpty();
        Column(csv, "loop--1--q").Should().Equal("1");
        Column(csv, "loop--2--q").Should().Equal("");
        Column(csv, "loop--3--q").Should().Equal("2");
    }

    // Select-to-rank: only the ranked choices get a panel item. Iterations take the source's labels (#1723).
    [Fact]
    public async Task Export_LoopOverARankingThatCopiesItsChoices_ExportsOneIterationPerCopiedChoice()
    {
        // Arrange
        var definition = Definition("""
            {"type":"checkbox","name":"occasions","choices":[{"value":"10","text":"Morning"},{"value":"20","text":"Evening"},{"value":"30","text":"Night"}]},
            {"type":"ranking","name":"rank","choicesFromQuestion":"occasions","choicesFromQuestionMode":"all","selectToRankEnabled":true}
            """, "rank");
        IReadOnlyList<FixtureResponse> responses =
        [
            new("r1", """
                {"rank":["30","10"],
                 "loop":[{"itemValue":"10","loopIndex":1,"q":"yes"},{"itemValue":"30","loopIndex":0,"q":"no"}]}
                """, null),
        ];

        // Act
        var (shoji, csv) = await ExportAsync(definition, responses);

        // Assert
        Subvariables(shoji, "loop--q").Should().Equal(
            ("loop--10--q", "Morning"), ("loop--20--q", "Evening"), ("loop--30--q", "Night"));
        CrunchImportCheck.Problems(shoji, csv).Should().BeEmpty();
        Column(csv, "loop--10--q").Should().Equal("1");
        Column(csv, "loop--20--q").Should().Equal("");
        Column(csv, "loop--30--q").Should().Equal("2");
    }

    // Hub carry forward offers the union of its sources, a value offered twice repeating once. The Shoji codebook lists
    // subvariables by alias; the flattener keeps source order (LoopSourceDriverTests).
    [Fact]
    public async Task Export_LoopOverACarryForwardFromTwoSources_RepeatsOverTheUnion()
    {
        // Arrange
        var definition = Definition("""
            {"type":"checkbox","name":"fruit","choices":["apple",{"value":"pear","text":"Pear (fruit)"}]},
            {"type":"dropdown","name":"orchard","choices":[{"value":"pear","text":"Pear (orchard)"},"plum"]},
            {"type":"checkbox","name":"liked","edxCarryForwardEnabled":true,"edxCarryForwardSources":["orchard","fruit"]}
            """, "liked");
        IReadOnlyList<FixtureResponse> responses =
        [
            new("r1", """{"liked":["plum"],"loop":[{"itemValue":"plum","loopIndex":0,"q":"yes"}]}""", null),
        ];

        // Act
        var (shoji, csv) = await ExportAsync(definition, responses);

        // Assert
        Subvariables(shoji, "loop--q").Should().Equal(
            ("loop--apple--q", "apple"), ("loop--pear--q", "Pear (orchard)"), ("loop--plum--q", "plum"));
        CrunchImportCheck.Problems(shoji, csv).Should().BeEmpty();
        Column(csv, "loop--plum--q").Should().Equal("1");
    }

    // A source the definition cannot tell (a data list until #1160) leaves the loop without iterations, as before.
    [Fact]
    public async Task Export_LoopOverACopyOfADataList_HasNoIterations()
    {
        // Arrange
        var definition = Definition("""
            {"type":"dropdown","name":"brand","edxDataListId":"1234"},
            {"type":"checkbox","name":"liked","choicesFromQuestion":"brand"}
            """, "liked");
        IReadOnlyList<FixtureResponse> responses = [new("r1", """{"liked":["x"]}""", null)];

        // Act
        var (shoji, csv) = await ExportAsync(definition, responses);

        // Assert
        Metadata(shoji).Keys.Should().NotContain(alias => alias.StartsWith("loop", StringComparison.Ordinal));
        CrunchImportCheck.Problems(shoji, csv).Should().BeEmpty();
    }

    // A loop over a source with its own choices and Other keeps an iteration for Other.
    [Fact]
    public async Task Export_LoopOverACheckboxWithItsOwnChoicesAndOther_KeepsTheOtherIteration()
    {
        // Arrange
        var definition = Definition("""
            {"type":"checkbox","name":"brands","choices":[{"value":"1","text":"Brand 1"}],"showOtherItem":true}
            """, "brands");
        IReadOnlyList<FixtureResponse> responses =
        [
            new("r1", """{"brands":["other"],"loop":[{"itemValue":"other","loopIndex":0,"q":"yes"}]}""", null),
        ];

        // Act
        var (shoji, csv) = await ExportAsync(definition, responses);

        // Assert
        Subvariables(shoji, "loop--q").Select(subvariable => subvariable.Alias).Should().Equal("loop--1--q", "loop--other--q");
        CrunchImportCheck.Problems(shoji, csv).Should().BeEmpty();
        Column(csv, "loop--other--q").Should().Equal("1");
    }

    private static string Definition(string sourceQuestions, string loopSource) =>
        $$"""
        {"pages":[{"name":"page1","elements":[{{sourceQuestions}},
          {"type":"paneldynamic","name":"loop","loopSource":["{{loopSource}}"],"templateElements":[{{LOOPED_QUESTION}}]}]}]}
        """;

    private static async Task<(string Shoji, string Csv)> ExportAsync(string definition, IReadOnlyList<FixtureResponse> responses)
    {
        var pipeline = PersonalizationPipeline.Compile(definition);
        pipeline.FlattenAll(definition, responses);
        var cancellationToken = TestContext.Current.CancellationToken;
        var shoji = await CrunchExport.ShojiCodebookAsync(pipeline.Entity(), cancellationToken, SHOJI_SETTINGS);
        var csv = await CrunchExport.CsvAsync(new(pipeline.Entity(), pipeline.ExportRows(responses), SHOJI_SETTINGS), cancellationToken);
        return (shoji, csv);
    }

    private static List<(string Alias, string Name)> Subvariables(string shoji, string alias) =>
        Metadata(shoji)[alias].GetProperty("subvariables").EnumerateArray()
            .Select(subvariable => (subvariable.GetProperty("alias").GetString()!, subvariable.GetProperty("name").GetString()!))
            .ToList();

    private static Dictionary<string, JsonElement> Metadata(string shoji)
    {
        using var document = JsonDocument.Parse(shoji);
        return document.RootElement.GetProperty("body").GetProperty("table").GetProperty("metadata").EnumerateObject()
            .ToDictionary(variable => variable.Name, variable => variable.Value.Clone(), StringComparer.Ordinal);
    }

    private static List<string> Column(string csv, string header)
    {
        var (headers, rows) = CrunchImportCheck.Read(csv);
        var index = headers.IndexOf(header);
        index.Should().BeGreaterThanOrEqualTo(0, $"the CSV has a '{header}' column");
        return rows.Select(row => row[index]).ToList();
    }
}
