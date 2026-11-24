using System.Text.Json;
using Endatix.Modules.Reporting.Features.FormSchema.FormSchema;
using Endatix.Modules.Reporting.Tests.Features.Export.Integrations.Crunch;
using Endatix.Modules.Reporting.Tests.Features.FormSchema.Personalization;

namespace Endatix.Modules.Reporting.Tests.Features.FormSchema;

public sealed class MatrixRevisionTests
{
    private const string ORIGINAL = """[80,90,{"value":2,"text":"Two"},{"value":"skip","text":"Skip"}]""";
    private const string REVISED = """[80,{"value":2.0,"text":"Two"},{"value":"skip","text":"Skip"},{"value":"added","text":"Added"}]""";

    private static string Definition(string columns) =>
        $$"""{"elements":[{"type":"matrix","name":"m","rows":["r"],"columns":{{columns}}}]}""";

    [Fact]
    public async Task Merge_AppendedAndRemovedCategories_KeepAllPublishedIdsAndDecodeOldAndNewRows()
    {
        // Arrange
        var pipeline = PersonalizationPipeline.Compile(Definition(ORIGINAL));
        FixtureResponse[] responses = [Response("a", "90"), Response("b", "80"), Response("c", "\"skip\""), Response("d", "\"added\"")];
        pipeline.FlattenAll(Definition(ORIGINAL), responses[..3]);

        // Act
        pipeline.Recompile(Definition(REVISED), FormSchemaCompileMode.Merge);
        pipeline.Flatten(Definition(REVISED), responses[3]);
        var schema = pipeline.Entity();
        var csv = await CrunchExport.CsvAsync(schema, pipeline.ExportRows(responses), TestContext.Current.CancellationToken);
        using var document = JsonDocument.Parse(await CrunchExport.ShojiCodebookAsync(schema, TestContext.Current.CancellationToken));
        var variable = document.RootElement.GetProperty("body").GetProperty("table").GetProperty("metadata").GetProperty("m");
        var categories = variable.GetProperty("categories").EnumerateArray().ToDictionary(c => c.GetProperty("id").GetRawText(), c => c.GetProperty("name").GetString());

        // Assert
        categories["1"].Should().Be("Two");
        categories["2"].Should().Be("Skip");
        categories["3"].Should().Be("80");
        categories["4"].Should().Be("90");
        categories["5"].Should().Be("Added");
        var alias = variable.GetProperty("subvariables")[0].GetProperty("alias").GetString()!;
        alias.Should().Be("m--r");
        var (header, rows) = CrunchImportCheck.Read(csv);
        rows.Select(r => categories[r[header.IndexOf(alias)]]).Should().Equal("90", "80", "Skip", "Added");
    }

    [Theory]
    [InlineData("2", null, "")]
    [InlineData("2", "{}", "")]
    [InlineData("2", "{\"m\":{\"r\":2.0}}", "1")]
    [InlineData("2", "{\"m\":{\"r\":\"skip\"}}", "2")]
    [InlineData("2", "{\"m\":{\"r\":\"unknown\"}}", "")]
    [InlineData("80", null, "3")]
    public async Task Export_CollidingFlattenedCell_RequiresMatchingSourceOnlyForAmbiguousValues(string flat, string? raw, string expected)
    {
        // Arrange
        var pipeline = PersonalizationPipeline.Compile(Definition(ORIGINAL));
        var response = Response("a", flat);
        pipeline.Flatten(Definition(ORIGINAL), response);
        var withoutEvidence = pipeline.ExportRows([response]).Single() with { SourceAnswersJson = null };
        var row = raw is null ? withoutEvidence : withoutEvidence.WithSourceAnswers(raw);

        // Act
        var csv = await CrunchExport.CsvAsync(pipeline.Entity(), [row], TestContext.Current.CancellationToken);

        // Assert
        var (header, rows) = CrunchImportCheck.Read(csv);
        rows.Single()[header.IndexOf("m--r")].Should().Be(expected);
    }

    [Fact]
    public async Task Export_EquivalentJsonNumbers_MatchExactlyAndRemainDistinctFromStrings()
    {
        // Arrange
        var definition = Definition("""[10.0,1.5E2,9007199254740992,9007199254740993,"10"]""");
        var pipeline = PersonalizationPipeline.Compile(definition);
        FixtureResponse[] responses = [Response("a", "10"), Response("b", "150"), Response("c", "9007199254740993.0"), Response("d", "\"10\"")];
        pipeline.FlattenAll(definition, responses);

        // Act
        var csv = await CrunchExport.CsvAsync(pipeline.Entity(), pipeline.ExportRows(responses), TestContext.Current.CancellationToken);

        // Assert
        var (header, rows) = CrunchImportCheck.Read(csv);
        using var book = JsonDocument.Parse(pipeline.ShojiCodebook());
        var matrix = book.RootElement.GetProperty("body").GetProperty("table").GetProperty("metadata").GetProperty("m");
        var ids = rows.Select(r => r[header.IndexOf(matrix.GetProperty("subvariables")[0].GetProperty("alias").GetString()!)]);
        var labels = matrix.GetProperty("categories").EnumerateArray().ToDictionary(c => c.GetProperty("id").GetRawText(), c => c.GetProperty("name").GetString());
        ids.Select(id => labels[id]).Should().Equal("10.0", "1.5E2", "9007199254740993", "10");
    }

    [Fact]
    public void Merge_UntypedLegacyLabels_CannotEstablishNumericCategoryIdentity()
    {
        // Arrange
        var pipeline = PersonalizationPipeline.Compile(Definition("""["Two","Skip"]"""));
        var stored = pipeline.Schema.CodebookJson;

        // Act
        var compile = () => pipeline.Recompile(Definition(REVISED), FormSchemaCompileMode.Merge);

        // Assert
        compile.Should().Throw<ReportingSchemaAliasConflictException>();
        pipeline.Schema.CodebookJson.Should().Be(stored);
    }

    [Theory]
    [InlineData("""{"type":"rating","name":"m"}""")]
    [InlineData("""{"type":"matrixdropdown","name":"m","rows":["r"],"columns":[{"name":"x","cellType":"text"}]}""")]
    public void Merge_ChangingQuestionTypeInEitherDirection_KeepsHistoricalColumnKeys(string replacement)
    {
        var radio = Definition(ORIGINAL);
        var other = """{"elements":[""" + replacement + "]}";
        var pipeline = PersonalizationPipeline.Compile(radio);
        var first = pipeline.Schema.FlatteningMap.Columns.Select(c => c.Key).ToArray();
        pipeline.Recompile(other, FormSchemaCompileMode.Merge);
        var second = pipeline.Schema.FlatteningMap.Columns.Select(c => c.Key).ToArray();
        second.Should().Contain(first);
        using var book = JsonDocument.Parse(pipeline.Schema.CodebookJson);
        book.RootElement.GetProperty("questions").GetProperty("m").GetProperty("surveyJsType").GetString().Should().NotBe("matrix");
        pipeline.Recompile(radio, FormSchemaCompileMode.Merge);
        pipeline.Schema.FlatteningMap.Columns.Select(c => c.Key).Should().Equal(second);
    }

    [Theory]
    [InlineData("[10,1E1]")]
    [InlineData("""[10,"a","a"]""")]
    public void Compile_AmbiguousTypedIdentity_RefusesTheDefinition(string columns)
    {
        var compile = () => PersonalizationPipeline.Compile(Definition(columns));
        compile.Should().Throw<ReportingSchemaAliasConflictException>();
    }

    private static FixtureResponse Response(string id, string token) =>
        new(id, """{"m":{"r":""" + token + "}}", null);
}
