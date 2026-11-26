using System.Text;
using System.Text.Json;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;
using Endatix.Modules.Reporting.Tests.Features.Export.Tabular;
using Endatix.Modules.Reporting.Tests.Features.FormSchema.FormSchema;
using Endatix.Modules.Reporting.Tests.Features.FormSchema.Personalization;

namespace Endatix.Modules.Reporting.Tests.Features.Export;

public sealed class ConfiguredChoiceFilesTests
{
    private const string SETTINGS = """{"specialChoiceCodes":{"none":99,"refused":98,"dontknow":97}}""";

    private static Task<byte[]> FileAsync(string format)
    {
        var definition = FormSchemaFixtureLoader.LoadText("special-choices-definition.json");
        var answers = FormSchemaFixtureLoader.LoadText("special-choices-submission.json");
        IReadOnlyList<FixtureResponse> responses = [new("r", answers, null)];
        var pipeline = PersonalizationPipeline.Compile(definition);
        pipeline.FlattenAll(definition, responses);
        return SubmissionFileExport.ExportAsync(new(format, pipeline.Entity(), pipeline.ExportRows(responses), SETTINGS),
            TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task Json_ConfiguredSelects_WritesNumbersAndKeepsMultiSelectIndicators()
    {
        // Act
        using var json = JsonDocument.Parse(await FileAsync("json"));
        var row = json.RootElement[0];

        // Assert
        row.GetProperty("dd").GetInt32().Should().Be(97);
        row.GetProperty("pd__0__pdRg").GetInt32().Should().Be(99);
        row.GetProperty("loop__puma__loopRg").GetInt32().Should().Be(98);
        row.GetProperty("tb__none").GetInt32().Should().Be(1);
        row.GetProperty("rg").GetString().Should().Be("other");
    }

    [Fact]
    public async Task Json_ValueNamePanel_CodesTheSelectAndKeepsAnInlineNone()
    {
        // Arrange
        const string definition = """
            {"pages":[{"elements":[{"type":"checkbox","name":"cars","valuePropertyName":"brand","choices":["a"]},
            {"type":"paneldynamic","name":"details","valueName":"cars","templateElements":[
              {"type":"dropdown","name":"model","choices":["one"],"showNoneItem":true},
              {"type":"dropdown","name":"note","choices":["none"]}]}]}]}
            """;
        IReadOnlyList<FixtureResponse> responses = [new("r", """{"cars":[{"brand":"a","model":"none","note":"none"}]}""", null)];
        var pipeline = PersonalizationPipeline.Compile(definition);
        pipeline.FlattenAll(definition, responses);

        // Act
        var bytes = await SubmissionFileExport.ExportAsync(new("json", pipeline.Entity(), pipeline.ExportRows(responses), SETTINGS),
            TestContext.Current.CancellationToken);

        // Assert
        using var json = JsonDocument.Parse(bytes);
        json.RootElement[0].GetProperty("a__model").GetInt32().Should().Be(99);
        json.RootElement[0].GetProperty("a__note").GetString().Should().Be("none");
    }

    [Fact]
    public async Task Csv_ConfiguredSelect_WritesCode()
    {
        // Act
        var lines = Encoding.UTF8.GetString(await FileAsync("csv")).Split('\n');
        var index = Array.IndexOf(lines[0].Trim().Split(','), "dd");

        // Assert
        lines[1].Split(',')[index].Should().Be("97");
    }

    [Fact]
    public async Task Xlsx_ConfiguredSelect_WritesNumericCell()
    {
        // Act
        using var workbook = SpreadsheetDocument.Open(new MemoryStream(await FileAsync("xlsx")), false);
        var rows = workbook.WorkbookPart!.WorksheetParts.First().Worksheet!.Descendants<Row>().ToArray();
        var index = rows[0].Elements<Cell>().Select(c => c.InnerText).ToList().IndexOf("dd");
        var cell = rows[1].Elements<Cell>().ElementAt(index);

        // Assert
        cell.DataType!.Value.Should().Be(CellValues.Number);
        cell.InnerText.Should().Be("97");
    }
}
