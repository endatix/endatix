using System.Text.Json;
using Endatix.Modules.Reporting.Contracts.Export;
using Endatix.Modules.Reporting.Features.Export.Integrations.Crunch.Shoji;
using Endatix.Modules.Reporting.Features.FormSchema.FormSchema;
using Endatix.Modules.Reporting.Tests.Features.FormSchema.FormSchema;
using FluentAssertions;

namespace Endatix.Modules.Reporting.Tests.Features.Export;

public sealed class ShojiCodebookGeneratorTests
{
    [Fact]
    public void Generate_WithAllQuestionsSchema_ProducesExpectedShojiCodebook()
    {
        // Arrange
        string definitionJson = FormSchemaFixtureLoader.LoadAllQuestionsText("all-questions-definition.json");
        JsonElement expectedShojiCodebook = FormSchemaFixtureLoader.LoadAllQuestionsExpectedShojiCodebook();
        FormSchemaCompiler compiler = new();
        FormSchemaCompileResult compiled = compiler.CompilePersisted(definitionJson);

        // Act
        string actualShojiCodebook = ShojiCodebookGenerator.Generate(
            compiled.FlatteningMapJson,
            compiled.CodebookJson,
            ExportFormatSettings.InterimCrunchKeySeparator);
        using JsonDocument actualDocument = JsonDocument.Parse(actualShojiCodebook);

        // Assert
        FormSchemaFixtureAssertions.AssertJsonMatchesExpected(
            actualDocument.RootElement,
            expectedShojiCodebook,
            because: "all-questions sample should generate the committed Shoji codebook golden output");
    }

    [Fact]
    public void Generate_Order_FollowsSystemColumnsThenSurveyAppearance()
    {
        // Arrange
        string definitionJson = FormSchemaFixtureLoader.LoadAllQuestionsText("all-questions-definition.json");
        FormSchemaCompiler compiler = new();
        FormSchemaCompileResult compiled = compiler.CompilePersisted(definitionJson);

        // Act
        using JsonDocument document = JsonDocument.Parse(
            ShojiCodebookGenerator.Generate(
                compiled.FlatteningMapJson,
                compiled.CodebookJson,
                ExportFormatSettings.InterimCrunchKeySeparator));
        List<string> order = document.RootElement
            .GetProperty("body")
            .GetProperty("table")
            .GetProperty("order")
            .EnumerateArray()
            .Select(element => element.GetString()!)
            .ToList();

        // Assert — definition walk, not alphabetical / writer-phase order
        order.Take(10).Should().Equal(
            "FormId",
            "Id",
            "IsComplete",
            "CreatedAt",
            "ModifiedAt",
            "StartedAt",
            "CompletedAt",
            "DurationSeconds",
            "SubmitterId",
            "SubmitterDisplayId");
        order.Skip(10).Take(6).Should().Equal(
            "qRadioGroup",
            "qRating",
            "qSlider",
            "qRangeSlider--min",
            "qRangeSlider--max",
            "qDropdown");
        order.Should().Contain("qLoop--qLoopColor");
        order.IndexOf("qLoop--adidas--qLoopBoolean").Should().BeLessThan(order.IndexOf("qLoop--qLoopColor"));
        order.IndexOf("qLoop--qLoopColor").Should().BeLessThan(order.IndexOf("qLoop--adidas--qLoopColor--other_text"));
    }

    [Fact]
    public void Generate_EmitsNativeCrunchEnvelopeWithFlatMetadataAndUniqueStringNames()
    {
        // Arrange
        string definitionJson = FormSchemaFixtureLoader.LoadAllQuestionsText("all-questions-definition.json");
        FormSchemaCompiler compiler = new();
        FormSchemaCompileResult compiled = compiler.CompilePersisted(definitionJson);

        // Act
        using JsonDocument document = JsonDocument.Parse(
            ShojiCodebookGenerator.Generate(
                compiled.FlatteningMapJson,
                compiled.CodebookJson,
                ExportFormatSettings.InterimCrunchKeySeparator));
        JsonElement root = document.RootElement;

        // Assert
        root.GetProperty("element").GetString().Should().Be("shoji:entity");
        JsonElement table = root.GetProperty("body").GetProperty("table");
        table.GetProperty("element").GetString().Should().Be("crunch:table");
        JsonElement metadata = table.GetProperty("metadata");

        metadata.TryGetProperty("version", out _).Should().BeFalse();
        metadata.TryGetProperty("variables", out _).Should().BeFalse();
        metadata.TryGetProperty("FormId", out _).Should().BeTrue();
        metadata.GetProperty("CreatedAt").GetProperty("resolution").GetString().Should().Be("s");
        metadata.GetProperty("qDropdown").GetProperty("name").ValueKind.Should().Be(JsonValueKind.String);

        HashSet<string> names = new(StringComparer.Ordinal);
        foreach (JsonProperty variable in metadata.EnumerateObject())
        {
            string name = variable.Value.GetProperty("name").GetString()!;
            names.Add(name).Should().BeTrue($"display name '{name}' must be unique");
        }
    }

    [Fact]
    public void Generate_MatrixValueOnlyRows_SubvariableNamesFallBackToRowValue()
    {
        // Arrange — reproduces endatix#914: value-only / blank-text matrix rows.
        string definitionJson = FormSchemaFixtureLoader.LoadText("matrix-value-only-rows-definition.json");
        FormSchemaCompiler compiler = new();
        FormSchemaCompileResult compiled = compiler.CompilePersisted(definitionJson);

        // Act
        using JsonDocument document = JsonDocument.Parse(
            ShojiCodebookGenerator.Generate(
                compiled.FlatteningMapJson,
                compiled.CodebookJson,
                ExportFormatSettings.InterimCrunchKeySeparator));
        JsonElement p7 = document.RootElement
            .GetProperty("body")
            .GetProperty("table")
            .GetProperty("metadata")
            .GetProperty("P7");

        // Assert
        p7.GetProperty("type").GetString().Should().Be("categorical_array");
        List<(string Alias, string Name)> subvariables = p7
            .GetProperty("subvariables")
            .EnumerateArray()
            .Select(item => (
                item.GetProperty("alias").GetString()!,
                item.GetProperty("name").GetString()!))
            .ToList();

        subvariables.Should().Equal(
            ("P7--Deprati", "Deprati"),
            ("P7--Etafashion", "Etafashion"),
            ("P7--Sukasa", "Sukasa"),
            ("P7--Pycca", "Pycca Stores"),
            ("P7--Todo Hogar", "Todo Hogar"),
            ("P7--Tiendas en línea / Páginas web", "Tiendas en línea / Páginas web"));

        subvariables.Should().OnlyContain(item => !string.IsNullOrWhiteSpace(item.Name));
    }

    [Fact]
    public void Generate_MatrixEmptyRowLabel_FallsBackToMatrixRowValue()
    {
        // Arrange — defense in depth for pre-fix persisted artifacts with blank rowLabel.
        string definitionJson = FormSchemaFixtureLoader.LoadText("matrix-value-only-rows-definition.json");
        FormSchemaCompileResult compiled = new FormSchemaCompiler().CompilePersisted(definitionJson);

        System.Text.Json.Nodes.JsonObject root =
            System.Text.Json.Nodes.JsonNode.Parse(compiled.CodebookJson)!.AsObject();
        System.Text.Json.Nodes.JsonObject columns = root["columns"]!.AsObject();
        foreach (KeyValuePair<string, System.Text.Json.Nodes.JsonNode?> column in columns)
        {
            System.Text.Json.Nodes.JsonObject columnObject = column.Value!.AsObject();
            columnObject["rowLabel"] = new System.Text.Json.Nodes.JsonObject
            {
                ["default"] = string.Empty,
            };
        }

        string mutatedCodebookJson = root.ToJsonString();

        // Act
        using JsonDocument document = JsonDocument.Parse(
            ShojiCodebookGenerator.Generate(
                compiled.FlatteningMapJson,
                mutatedCodebookJson,
                ExportFormatSettings.InterimCrunchKeySeparator));
        List<string> names = document.RootElement
            .GetProperty("body")
            .GetProperty("table")
            .GetProperty("metadata")
            .GetProperty("P7")
            .GetProperty("subvariables")
            .EnumerateArray()
            .Select(item => item.GetProperty("name").GetString()!)
            .ToList();

        // Assert — ignores blank rowLabel; uses matrixRowValue (not display text).
        names.Should().Equal(
            "Deprati",
            "Etafashion",
            "Sukasa",
            "Pycca",
            "Todo Hogar",
            "Tiendas en línea / Páginas web");
    }

    [Fact]
    public void Generate_TrailingWhitespaceInChoiceValues_StripsFromAliasesAndNames()
    {
        // Arrange — Crunch rejects subvariable aliases with trailing spaces
        // ("Expected column P11--Visitando...  not found" when CSV headers are trimmed).
        string definitionJson = FormSchemaFixtureLoader.LoadText("trailing-whitespace-choices-definition.json");
        FormSchemaCompileResult compiled = new FormSchemaCompiler().CompilePersisted(definitionJson);

        // Act
        using JsonDocument document = JsonDocument.Parse(
            ShojiCodebookGenerator.Generate(
                compiled.FlatteningMapJson,
                compiled.CodebookJson,
                ExportFormatSettings.InterimCrunchKeySeparator));
        JsonElement metadata = document.RootElement
            .GetProperty("body")
            .GetProperty("table")
            .GetProperty("metadata");

        // Assert — checkbox multiple_response subvariable aliases have no trailing whitespace
        List<(string Alias, string Name)> p11 = metadata
            .GetProperty("P11")
            .GetProperty("subvariables")
            .EnumerateArray()
            .Select(item => (
                item.GetProperty("alias").GetString()!,
                item.GetProperty("name").GetString()!))
            .ToList();

        p11.Should().Equal(
            ("P11--Redes sociales", "Redes sociales"),
            ("P11--Visitando los centros comerciales", "Visitando los centros comerciales"),
            ("P11--Otros", "Otros"));
        p11.Should().OnlyContain(item =>
            item.Alias == item.Alias.Trim() && item.Name == item.Name.Trim());

        // Assert — matrix row aliases and category names are trimmed
        List<string> p4Aliases = metadata
            .GetProperty("P4")
            .GetProperty("subvariables")
            .EnumerateArray()
            .Select(item => item.GetProperty("alias").GetString()!)
            .ToList();
        p4Aliases.Should().Equal("P4--Colchones", "P4--Almohadas");

        // Whitespace-valued row must still resolve distinct display text via FindMatrixRowElement.
        string colchonesLabel = metadata
            .GetProperty("P4")
            .GetProperty("subvariables")
            .EnumerateArray()
            .Single(item => item.GetProperty("alias").GetString() == "P4--Colchones")
            .GetProperty("name")
            .GetString()!;
        colchonesLabel.Should().Be("Mattresses (display)");

        List<string> p4Categories = metadata
            .GetProperty("P4")
            .GetProperty("categories")
            .EnumerateArray()
            .Select(item => item.GetProperty("name").GetString()!)
            .ToList();
        p4Categories.Should().Equal("Marca", "Precio");
        p4Categories.Should().OnlyContain(name => name == name.Trim());

        // Assert — FlatteningMap keys are also trimmed (source of truth)
        compiled.FlatteningMap.Columns.Select(column => column.Key).Should().Contain(
            "P11__Visitando los centros comerciales",
            "P11__Otros",
            "P4__Colchones");
        compiled.FlatteningMap.Columns.Select(column => column.Key)
            .Should()
            .NotContain(key => key != key.Trim());

        // Assert — persisted codebook column rowLabel keeps the distinct SurveyJS text
        using JsonDocument codebook = JsonDocument.Parse(compiled.CodebookJson);
        codebook.RootElement
            .GetProperty("columns")
            .GetProperty("P4__Colchones")
            .GetProperty("rowLabel")
            .GetProperty("default")
            .GetString()
            .Should()
            .Be("Mattresses (display)");
    }
}
