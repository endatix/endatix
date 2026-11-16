using System.Text.Json;
using Endatix.Modules.Reporting.Contracts.Export;
using Endatix.Modules.Reporting.Features.Export.Integrations.Crunch.Shoji;
using Endatix.Modules.Reporting.Features.FormSchema.FormSchema;
using Endatix.Modules.Reporting.Tests.Features.FormSchema.FormSchema;

namespace Endatix.Modules.Reporting.Tests.Features.FormSchema;

/// <summary>
/// Export edge cases reviewed as whole documents: every folder under <c>Fixtures/Scenarios/</c> holds one definition
/// and the format-neutral and Shoji codebooks it compiles to. Adding a scenario is described in <c>Fixtures/README.md</c>.
/// </summary>
[Trait("Category", "Golden")]
public sealed class ScenarioGoldenTests
{
    public static TheoryData<string> Scenarios() => new(ScenarioGoldens.Names(ScenarioGoldens.OutputRoot));

    [Theory]
    [MemberData(nameof(Scenarios))]
    public void Scenario_CompilesToItsGoldenCodebooks(string scenario)
    {
        // Arrange
        var folder = Path.Combine(ScenarioGoldens.OutputRoot, scenario);

        // Act
        var (codebook, shojiCodebook) = ScenarioGoldens.Compile(folder);

        // Assert
        FormSchemaFixtureAssertions.AssertJsonMatchesExpected(
            Parse(codebook),
            Parse(File.ReadAllText(Path.Combine(folder, ScenarioGoldens.CodebookFile))),
            because: $"scenario '{scenario}' should compile to its committed codebook");
        FormSchemaFixtureAssertions.AssertJsonMatchesExpected(
            Parse(shojiCodebook),
            Parse(File.ReadAllText(Path.Combine(folder, ScenarioGoldens.ShojiCodebookFile))),
            because: $"scenario '{scenario}' should export its committed Shoji codebook");
    }

    private static JsonElement Parse(string json)
    {
        using var document = JsonDocument.Parse(json);
        return document.RootElement.Clone();
    }
}

/// <summary>Compiles and writes the scenario goldens; the manual regeneration test calls <see cref="RegenerateAll"/>.</summary>
internal static class ScenarioGoldens
{
    internal const string DefinitionFile = "definition.json";
    internal const string CodebookFile = "expected-codebook.json";
    internal const string ShojiCodebookFile = "expected-shoji-codebook.json";

    private static readonly JsonSerializerOptions _indented = new() { WriteIndented = true };

    internal static string OutputRoot => Path.Combine(
        AppContext.BaseDirectory, "Features", "FormSchema", "FlattenedFormDefinition", "Fixtures", "Scenarios");

    internal static string SourceRoot => Path.Combine(FormSchemaFixtureLoader.SourceFixturesRoot, "Scenarios");

    internal static IEnumerable<string> Names(string root) =>
        Directory.EnumerateDirectories(root)
            .Where(folder => File.Exists(Path.Combine(folder, DefinitionFile)))
            .Select(Path.GetFileName)
            .OfType<string>()
            .Order(StringComparer.Ordinal);

    internal static (string Codebook, string ShojiCodebook) Compile(string folder)
    {
        var compiled = new FormSchemaCompiler().CompilePersisted(File.ReadAllText(Path.Combine(folder, DefinitionFile)));
        var shojiCodebook = ShojiCodebookGenerator.Generate(
            compiled.FlatteningMapJson,
            compiled.CodebookJson,
            ExportFormatSettings.InterimCrunchKeySeparator);
        return (compiled.CodebookJson, shojiCodebook);
    }

    internal static void RegenerateAll()
    {
        foreach (var scenario in Names(SourceRoot))
        {
            var folder = Path.Combine(SourceRoot, scenario);
            var (codebook, shojiCodebook) = Compile(folder);
            Write(Path.Combine(folder, CodebookFile), codebook);
            Write(Path.Combine(folder, ShojiCodebookFile), shojiCodebook);
        }
    }

    private static void Write(string path, string json)
    {
        using var document = JsonDocument.Parse(json);
        File.WriteAllText(path, JsonSerializer.Serialize(document.RootElement, _indented) + Environment.NewLine);
    }
}
