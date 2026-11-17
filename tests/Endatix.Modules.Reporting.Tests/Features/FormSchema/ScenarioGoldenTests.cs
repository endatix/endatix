using System.Globalization;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;
using Endatix.Modules.Reporting.Features.ExportFormats;
using Endatix.Modules.Reporting.Features.FlattenedSubmission;
using Endatix.Modules.Reporting.Tests.Features.Export.Integrations.Crunch;
using Endatix.Modules.Reporting.Tests.Features.Export.Tabular;
using Endatix.Modules.Reporting.Tests.Features.FormSchema.FormSchema;
using Endatix.Modules.Reporting.Tests.Features.FormSchema.Personalization;

namespace Endatix.Modules.Reporting.Tests.Features.FormSchema;

/// <summary>
/// Export edge cases reviewed as whole documents: every folder under <c>Fixtures/Scenarios/</c> holds one definition
/// and the files it exports to, and the test compares each <c>expected-*</c> file the folder has. Adding a scenario is
/// described in <c>Fixtures/README.md</c>.
/// </summary>
[Trait("Category", "Golden")]
public sealed class ScenarioGoldenTests
{
    public static TheoryData<string> Scenarios() => new(ScenarioGoldens.Names(ScenarioGoldens.OutputRoot));

    public static TheoryData<string> ScenariosWithASubmission() =>
        new(ScenarioGoldens.Names(ScenarioGoldens.OutputRoot).Where(scenario =>
            File.Exists(Path.Combine(ScenarioGoldens.OutputRoot, scenario, ScenarioGoldens.SUBMISSION_FILE))));

    [Theory]
    [MemberData(nameof(Scenarios))]
    public async Task Scenario_ExportsItsGoldenFiles(string scenario)
    {
        // Arrange
        var folder = Path.Combine(ScenarioGoldens.OutputRoot, scenario);

        // Act
        var exported = await ScenarioGoldens.ExportAsync(folder, TestContext.Current.CancellationToken);

        // Assert
        ScenarioGoldens.ExpectedFiles(folder).Should().BeEquivalentTo(
            exported.Keys,
            because: $"scenario '{scenario}' should commit a golden for every file it exports, and only those");
        foreach (var (file, content) in exported)
        {
            ScenarioGoldens.Normalize(file, content).Should().Be(
                File.ReadAllText(Path.Combine(folder, file)),
                because: $"scenario '{scenario}' should export its committed {file}");
        }
    }

    [Theory]
    [MemberData(nameof(ScenariosWithASubmission))]
    public async Task Scenario_WithASubmission_ImportsIntoCrunch(string scenario)
    {
        // Arrange
        var folder = Path.Combine(ScenarioGoldens.OutputRoot, scenario);
        var exported = await ScenarioGoldens.ExportAsync(folder, TestContext.Current.CancellationToken);

        // Act
        var problems = CrunchImportCheck.Problems(
            exported[ScenarioGoldens.SHOJI_CODEBOOK_FILE],
            exported[ScenarioGoldens.CSV_SHOJI_FILE]);

        // Assert
        problems.Should().BeEmpty();
    }
}

/// <summary>
/// Exports and writes the scenario goldens; the manual regeneration test calls <see cref="RegenerateAll"/>. A scenario
/// without a <c>submission.json</c> exports its two codebooks; one with it exports every format.
/// </summary>
internal static class ScenarioGoldens
{
    internal const string DEFINITION_FILE = "definition.json";
    internal const string SUBMISSION_FILE = "submission.json";
    internal const string CODEBOOK_FILE = "expected-codebook.json";
    internal const string SHOJI_CODEBOOK_FILE = "expected-shoji-codebook.json";
    internal const string FLATTENING_MAP_FILE = "expected-flattening-map.json";
    internal const string FLAT_FILE = "expected-flat.json";
    internal const string CSV_FILE = "expected-csv.csv";
    internal const string CSV_SHOJI_FILE = "expected-csv-shoji.csv";
    internal const string JSON_FILE = "expected-json.json";
    internal const string XLSX_FILE = "expected-xlsx.json";

    private const string EXPECTED_PREFIX = "expected-";
    private const string RESPONSE_ID = "r1";

    private static readonly JsonSerializerOptions _indented = new()
    {
        WriteIndented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    internal static string OutputRoot => Path.Combine(
        AppContext.BaseDirectory, "Features", "FormSchema", "FlattenedFormDefinition", "Fixtures", "Scenarios");

    internal static string SourceRoot => Path.Combine(FormSchemaFixtureLoader.SourceFixturesRoot, "Scenarios");

    internal static IEnumerable<string> Names(string root) =>
        Directory.EnumerateDirectories(root)
            .Where(folder => File.Exists(Path.Combine(folder, DEFINITION_FILE)))
            .Select(Path.GetFileName)
            .OfType<string>()
            .Order(StringComparer.Ordinal);

    internal static IReadOnlyList<string> ExpectedFiles(string folder) =>
        Directory.EnumerateFiles(folder, EXPECTED_PREFIX + "*")
            .Select(Path.GetFileName)
            .OfType<string>()
            .Order(StringComparer.Ordinal)
            .ToList();

    /// <summary>
    /// A file as its golden is committed: JSON indented, in the order it was written, with markup left readable;
    /// anything else byte for byte, so the csv record terminator is pinned too.
    /// </summary>
    internal static string Normalize(string file, string content)
    {
        if (!file.EndsWith(".json", StringComparison.Ordinal))
        {
            return content;
        }

        using var document = JsonDocument.Parse(content);
        return JsonSerializer.Serialize(document.RootElement, _indented) + "\n";
    }

    /// <summary>Every file the scenario exports, by its golden file name.</summary>
    internal static async Task<IReadOnlyDictionary<string, string>> ExportAsync(string folder, CancellationToken cancellationToken)
    {
        var definition = File.ReadAllText(Path.Combine(folder, DEFINITION_FILE));
        var pipeline = PersonalizationPipeline.Compile(definition);
        Dictionary<string, string> files = new(StringComparer.Ordinal)
        {
            [CODEBOOK_FILE] = pipeline.Schema.CodebookJson,
            [SHOJI_CODEBOOK_FILE] = pipeline.ShojiCodebook(),
        };

        var submissionPath = Path.Combine(folder, SUBMISSION_FILE);
        if (!File.Exists(submissionPath))
        {
            return files;
        }

        FixtureResponse response = new(RESPONSE_ID, File.ReadAllText(submissionPath), null);
        pipeline.FlattenAll(definition, [response]);
        var schema = pipeline.Entity(SubmissionFileExport.FORM_ID);
        var rows = pipeline.ExportRows([response]);

        files[FLATTENING_MAP_FILE] = pipeline.Schema.FlatteningMapJson;
        files[FLAT_FILE] = FlattenedSubmissionFlattener.ToJson(pipeline.Schema.FlatteningMap, pipeline.Rows[RESPONSE_ID]);
        files[CSV_FILE] = await TextAsync("csv", DefaultExportFormats.Csv.SettingsJson);
        files[CSV_SHOJI_FILE] = await TextAsync("csv-shoji", CrunchExport.SHOJI_FORMAT_SETTINGS);
        files[JSON_FILE] = await TextAsync("json", DefaultExportFormats.Json.SettingsJson);
        files[XLSX_FILE] = XlsxCells(await ExportFileAsync("xlsx", DefaultExportFormats.Xlsx.SettingsJson));
        return files;

        Task<byte[]> ExportFileAsync(string format, string settingsJson) =>
            SubmissionFileExport.ExportAsync(new(format, schema, rows, settingsJson), cancellationToken);

        async Task<string> TextAsync(string format, string settingsJson) =>
            Encoding.UTF8.GetString(await ExportFileAsync(format, settingsJson));
    }

    internal static async Task RegenerateAllAsync(CancellationToken cancellationToken)
    {
        foreach (var scenario in Names(SourceRoot))
        {
            var folder = Path.Combine(SourceRoot, scenario);
            foreach (var (file, content) in await ExportAsync(folder, cancellationToken))
            {
                Write(Path.Combine(folder, file), content);
            }
        }
    }

    // The workbook's cells as text, sheet by sheet and row by row, so a golden diff reads like the sheet.
    private static string XlsxCells(byte[] workbook)
    {
        using var document = SpreadsheetDocument.Open(new MemoryStream(workbook), isEditable: false);
        var workbookPart = document.WorkbookPart!;
        var sharedStrings = workbookPart.SharedStringTablePart?.SharedStringTable?.Elements<SharedStringItem>()
            .Select(item => item.InnerText).ToList() ?? [];
        Dictionary<string, List<List<string>>> sheets = new(StringComparer.Ordinal);
        foreach (var sheet in workbookPart.Workbook!.Sheets!.Elements<Sheet>())
        {
            var worksheet = (WorksheetPart)workbookPart.GetPartById(sheet.Id!.Value!);
            sheets[sheet.Name!.Value!] = worksheet.Worksheet!.Descendants<Row>()
                .Select(row => row.Elements<Cell>().Select(cell => CellText(cell, sharedStrings)).ToList())
                .ToList();
        }

        return JsonSerializer.Serialize(sheets);
    }

    // "<type>:<text>", plus "@<style>" when the cell has one: the exporter decides text, number, boolean and date, and
    // a change there must show in the golden even when the text stays the same.
    private static string CellText(Cell cell, IReadOnlyList<string> sharedStrings)
    {
        var type = cell.DataType?.Value;
        var text = type == CellValues.SharedString
            ? sharedStrings[int.Parse(cell.CellValue!.Text, CultureInfo.InvariantCulture)]
            : cell.InlineString?.InnerText ?? cell.CellValue?.Text ?? string.Empty;
        var style = cell.StyleIndex is { } index ? $"@{index.Value}" : string.Empty;
        return $"{CellType(type)}{style}:{text}";
    }

    private static string CellType(CellValues? type) =>
        type is null || type == CellValues.Number ? "n"
        : type == CellValues.Boolean ? "b"
        : type == CellValues.InlineString || type == CellValues.SharedString || type == CellValues.String ? "s"
        : type.ToString()!;

    private static void Write(string path, string content) =>
        File.WriteAllText(path, Normalize(Path.GetFileName(path), content));
}
