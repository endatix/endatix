using System.Text.Json;
using Endatix.Core.Entities;
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
        var definitionJson = FormSchemaFixtureLoader.LoadAllQuestionsText("all-questions-definition.json");
        var expectedShojiCodebook = FormSchemaFixtureLoader.LoadAllQuestionsExpectedShojiCodebook();
        FormSchemaCompiler compiler = new();
        var compiled = compiler.CompilePersisted(definitionJson);

        // Act
        var actualShojiCodebook = ShojiCodebookGenerator.Generate(
            compiled.FlatteningMapJson,
            compiled.CodebookJson,
            ExportFormatSettings.InterimCrunchKeySeparator);
        using var actualDocument = JsonDocument.Parse(actualShojiCodebook);

        // Assert
        FormSchemaFixtureAssertions.AssertJsonMatchesExpected(
            actualDocument.RootElement,
            expectedShojiCodebook,
            because: "all-questions sample should generate the committed Shoji codebook golden output");
    }

    [Fact]
    public void Generate_WritesCollectionStatusAsTextSystemVariable()
    {
        // Arrange
        const string definitionJson = """
            {"pages":[{"name":"page1","elements":[{"type":"text","name":"qName"}]}]}
            """;
        var compiled = new FormSchemaCompiler().CompilePersisted(definitionJson);

        // Act
        using var document = JsonDocument.Parse(
            ShojiCodebookGenerator.Generate(
                compiled.FlatteningMapJson,
                compiled.CodebookJson,
                ExportFormatSettings.InterimCrunchKeySeparator));
        var variable = document.RootElement
            .GetProperty("body").GetProperty("table").GetProperty("metadata")
            .GetProperty("CollectionStatus");

        // Assert — same alias, name and type as the legacy export_form_metadata_shoji_v2
        variable.GetProperty("type").GetString().Should().Be("text");
        variable.GetProperty("alias").GetString().Should().Be("CollectionStatus");
        variable.GetProperty("name").GetString().Should().Be("Collection Status");
    }

    [Fact]
    public void Generate_TagboxWithoutChoices_WritesTextVariableForValuesColumn()
    {
        const string definitionJson = """
            {"pages":[{"name":"page1","elements":[{"type":"tagbox","name":"questionCities","title":"Cities","choicesLazyLoadEnabled":true}]}]}
            """;
        var compiled = new FormSchemaCompiler().CompilePersisted(definitionJson);

        using var document = JsonDocument.Parse(
            ShojiCodebookGenerator.Generate(
                compiled.FlatteningMapJson,
                compiled.CodebookJson,
                ExportFormatSettings.InterimCrunchKeySeparator));
        var table = document.RootElement.GetProperty("body").GetProperty("table");

        table.GetProperty("metadata").GetProperty("questionCities").GetProperty("type").GetString()
            .Should().Be("text");
        table.GetProperty("order").EnumerateArray().Select(e => e.GetString())
            .Should().Contain("questionCities");
    }

    [Fact]
    public void Generate_SeveralCalculatedValues_KeepDefinitionOrderAfterSystemColumns()
    {
        // Arrange
        const string definitionJson = """
            {
              "pages":[{"name":"page1","elements":[{"type":"text","name":"qName"}]}],
              "calculatedValues":[
                {"name":"totalAmount","expression":"1 + 1","includeIntoResult":true},
                {"name":"averageAmount","expression":"2 + 2","includeIntoResult":true}
              ]
            }
            """;
        var compiled = new FormSchemaCompiler().CompilePersisted(definitionJson);

        // Act
        var order = ReadOrder(GenerateTable(compiled));

        // Assert
        order.Skip(SubmissionExportRow.SystemColumns.Count).Should().Equal("totalAmount", "averageAmount", "qName");
    }

    [Fact]
    public void Generate_CodebookCompiledBeforeCalculatedValuesWereModelled_WritesNoCalculatedVariable()
    {
        // Arrange
        const string definitionJson = """
            {
              "pages":[{"name":"page1","elements":[{"type":"text","name":"qName"}]}],
              "calculatedValues":[{"name":"totalAmount","expression":"1","includeIntoResult":true}]
            }
            """;
        var compiled = new FormSchemaCompiler().CompilePersisted(definitionJson);
        var staleCodebookJson = compiled.CodebookJson.Replace(
            "\"exportShape\":\"calculated\"",
            "\"exportShape\":\"scalar\"");

        // Act
        using var document = JsonDocument.Parse(
            ShojiCodebookGenerator.Generate(
                compiled.FlatteningMapJson,
                staleCodebookJson,
                ExportFormatSettings.InterimCrunchKeySeparator));

        // Assert: the codebook decides; such a form needs a recompile (#1170).
        staleCodebookJson.Should().NotBe(compiled.CodebookJson);
        ReadOrder(document.RootElement.GetProperty("body").GetProperty("table")).Should().NotContain("totalAmount");
    }

    [Fact]
    public void Generate_CalculatedValueNamedLikeGroupedQuestion_KeepsQuestionVariable()
    {
        // Arrange
        const string definitionJson = """
            {
              "pages":[{"name":"page1","elements":[
                {"type":"checkbox","name":"colors","title":"Colors","choices":["red","blue"]},
                {"type":"matrix","name":"qMatrix","title":"Matrix","columns":["a","b"],"rows":["r1","r2"]}
              ]}],
              "calculatedValues":[
                {"name":"colors","expression":"1","includeIntoResult":true},
                {"name":"qMatrix","expression":"1","includeIntoResult":true}
              ]
            }
            """;
        var compiled = new FormSchemaCompiler().CompilePersisted(definitionJson);

        // Act
        var table = GenerateTable(compiled);
        var metadata = table.GetProperty("metadata");
        var order = ReadOrder(table);

        // Assert
        metadata.GetProperty("colors").GetProperty("type").GetString().Should().Be("multiple_response");
        metadata.GetProperty("qMatrix").GetProperty("type").GetString().Should().Be("categorical_array");
        order.Skip(SubmissionExportRow.SystemColumns.Count).Should().Equal("colors", "qMatrix");
    }

    [Fact]
    public void Generate_CalculatedValueNamedLikeLeafAliasQuestion_WritesBothVariables()
    {
        // Arrange
        const string definitionJson = """
            {
              "pages":[{"name":"page1","elements":[
                {"type":"ranking","name":"qRanking","title":"Rank the fruit","choices":["apple","pear"]},
                {"type":"multipletext","name":"qMultipleText","items":[{"name":"phone"}]}
              ]}],
              "calculatedValues":[
                {"name":"qRanking","expression":"1","includeIntoResult":true},
                {"name":"qMultipleText","expression":"1","includeIntoResult":true}
              ]
            }
            """;
        var compiled = new FormSchemaCompiler().CompilePersisted(definitionJson);

        // Act
        var table = GenerateTable(compiled);
        var metadata = table.GetProperty("metadata");
        var order = ReadOrder(table);

        // Assert
        metadata.GetProperty("qRanking").GetProperty("type").GetString().Should().Be("text");
        metadata.GetProperty("qRanking").GetProperty("name").GetString().Should().Be("qRanking");
        metadata.GetProperty("qMultipleText").GetProperty("type").GetString().Should().Be("text");
        order.Skip(SubmissionExportRow.SystemColumns.Count).Take(2).Should().Equal("qRanking", "qMultipleText");
        order.Should().Contain(["qRanking--apple", "qMultipleText--phone"]);
    }

    [Fact]
    public void Generate_CalculatedValueNamedLikeQuestionTitle_KeepsQuestionDisplayName()
    {
        // Arrange
        const string definitionJson = """
            {
              "pages":[{"name":"page1","elements":[{"type":"text","name":"qTotal","title":"totalAmount"}]}],
              "calculatedValues":[{"name":"totalAmount","expression":"1","includeIntoResult":true}]
            }
            """;
        var compiled = new FormSchemaCompiler().CompilePersisted(definitionJson);

        // Act
        var metadata = GenerateTable(compiled).GetProperty("metadata");

        // Assert
        metadata.GetProperty("qTotal").GetProperty("name").GetString().Should().Be("totalAmount");
        metadata.GetProperty("totalAmount").GetProperty("name").GetString().Should().Be("totalAmount -- totalAmount");
    }

    [Fact]
    public void Generate_WithDatasetMetadata_WritesBodyNameAndDescription()
    {
        var definitionJson = FormSchemaFixtureLoader.LoadText("simple-definition.json");
        var compiled = new FormSchemaCompiler().CompilePersisted(definitionJson);

        using var document = JsonDocument.Parse(
            ShojiCodebookGenerator.Generate(
                compiled.FlatteningMapJson,
                compiled.CodebookJson,
                ExportFormatSettings.InterimCrunchKeySeparator,
                datasetName: "ACME Wave 1",
                datasetDescription: "Panel export for ACME"));
        var body = document.RootElement.GetProperty("body");

        body.GetProperty("name").GetString().Should().Be("ACME Wave 1");
        body.GetProperty("description").GetString().Should().Be("Panel export for ACME");
    }

    [Fact]
    public void Generate_WithoutDatasetMetadata_KeepsDefaultBodyNameAndDescription()
    {
        var definitionJson = FormSchemaFixtureLoader.LoadText("simple-definition.json");
        var compiled = new FormSchemaCompiler().CompilePersisted(definitionJson);

        using var document = JsonDocument.Parse(
            ShojiCodebookGenerator.Generate(
                compiled.FlatteningMapJson,
                compiled.CodebookJson,
                ExportFormatSettings.InterimCrunchKeySeparator));
        var body = document.RootElement.GetProperty("body");

        body.GetProperty("name").GetString().Should().Be("Form export");
        body.GetProperty("description").GetString().Should().Be("Shoji codebook metadata");
    }

    [Fact]
    public void Generate_Order_FollowsSystemColumnsThenSurveyAppearance()
    {
        // Arrange
        var definitionJson = FormSchemaFixtureLoader.LoadAllQuestionsText("all-questions-definition.json");
        FormSchemaCompiler compiler = new();
        var compiled = compiler.CompilePersisted(definitionJson);

        // Act
        using var document = JsonDocument.Parse(
            ShojiCodebookGenerator.Generate(
                compiled.FlatteningMapJson,
                compiled.CodebookJson,
                ExportFormatSettings.InterimCrunchKeySeparator));
        var order = document.RootElement
            .GetProperty("body")
            .GetProperty("table")
            .GetProperty("order")
            .EnumerateArray()
            .Select(element => element.GetString()!)
            .ToList();

        // Assert — calculated values after the system columns, then the definition walk (not
        // alphabetical / writer-phase order)
        order.Take(11).Should().Equal(
            "FormId",
            "Id",
            "IsComplete",
            "CollectionStatus",
            "CreatedAt",
            "ModifiedAt",
            "StartedAt",
            "CompletedAt",
            "DurationSeconds",
            "SubmitterId",
            "SubmitterDisplayId");
        order.Skip(11).Take(7).Should().Equal(
            "qCalculatedTotal",
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
        var definitionJson = FormSchemaFixtureLoader.LoadAllQuestionsText("all-questions-definition.json");
        FormSchemaCompiler compiler = new();
        var compiled = compiler.CompilePersisted(definitionJson);

        // Act
        using var document = JsonDocument.Parse(
            ShojiCodebookGenerator.Generate(
                compiled.FlatteningMapJson,
                compiled.CodebookJson,
                ExportFormatSettings.InterimCrunchKeySeparator));
        var root = document.RootElement;

        // Assert
        root.GetProperty("element").GetString().Should().Be("shoji:entity");
        var table = root.GetProperty("body").GetProperty("table");
        table.GetProperty("element").GetString().Should().Be("crunch:table");
        var metadata = table.GetProperty("metadata");

        metadata.TryGetProperty("version", out _).Should().BeFalse();
        metadata.TryGetProperty("variables", out _).Should().BeFalse();
        metadata.TryGetProperty("FormId", out _).Should().BeTrue();
        metadata.GetProperty("CreatedAt").GetProperty("resolution").GetString().Should().Be("s");
        metadata.GetProperty("qDropdown").GetProperty("name").ValueKind.Should().Be(JsonValueKind.String);

        HashSet<string> names = new(StringComparer.Ordinal);
        foreach (var variable in metadata.EnumerateObject())
        {
            var name = variable.Value.GetProperty("name").GetString()!;
            names.Add(name).Should().BeTrue($"display name '{name}' must be unique");
        }
    }

    [Fact]
    public void Generate_MatrixValueOnlyRows_SubvariableNamesFallBackToRowValue()
    {
        // Arrange — reproduces endatix#914: value-only / blank-text matrix rows.
        var definitionJson = FormSchemaFixtureLoader.LoadText("matrix-value-only-rows-definition.json");
        FormSchemaCompiler compiler = new();
        var compiled = compiler.CompilePersisted(definitionJson);

        // Act
        using var document = JsonDocument.Parse(
            ShojiCodebookGenerator.Generate(
                compiled.FlatteningMapJson,
                compiled.CodebookJson,
                ExportFormatSettings.InterimCrunchKeySeparator));
        var p7 = document.RootElement
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
        var definitionJson = FormSchemaFixtureLoader.LoadText("matrix-value-only-rows-definition.json");
        var compiled = new FormSchemaCompiler().CompilePersisted(definitionJson);

        var root =
            System.Text.Json.Nodes.JsonNode.Parse(compiled.CodebookJson)!.AsObject();
        var columns = root["columns"]!.AsObject();
        foreach (var column in columns)
        {
            var columnObject = column.Value!.AsObject();
            columnObject["rowLabel"] = new System.Text.Json.Nodes.JsonObject
            {
                ["default"] = string.Empty,
            };
        }

        var mutatedCodebookJson = root.ToJsonString();

        // Act
        using var document = JsonDocument.Parse(
            ShojiCodebookGenerator.Generate(
                compiled.FlatteningMapJson,
                mutatedCodebookJson,
                ExportFormatSettings.InterimCrunchKeySeparator));
        var names = document.RootElement
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
        var definitionJson = FormSchemaFixtureLoader.LoadText("trailing-whitespace-choices-definition.json");
        var compiled = new FormSchemaCompiler().CompilePersisted(definitionJson);

        // Act
        using var document = JsonDocument.Parse(
            ShojiCodebookGenerator.Generate(
                compiled.FlatteningMapJson,
                compiled.CodebookJson,
                ExportFormatSettings.InterimCrunchKeySeparator));
        var metadata = document.RootElement
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
        var p4Aliases = metadata
            .GetProperty("P4")
            .GetProperty("subvariables")
            .EnumerateArray()
            .Select(item => item.GetProperty("alias").GetString()!)
            .ToList();
        p4Aliases.Should().Equal("P4--Colchones", "P4--Almohadas");

        // Whitespace-valued row must still resolve distinct display text via FindMatrixRowElement.
        var colchonesLabel = metadata
            .GetProperty("P4")
            .GetProperty("subvariables")
            .EnumerateArray()
            .Single(item => item.GetProperty("alias").GetString() == "P4--Colchones")
            .GetProperty("name")
            .GetString()!;
        colchonesLabel.Should().Be("Mattresses (display)");

        var p4Categories = metadata
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
        using var codebook = JsonDocument.Parse(compiled.CodebookJson);
        codebook.RootElement
            .GetProperty("columns")
            .GetProperty("P4__Colchones")
            .GetProperty("rowLabel")
            .GetProperty("default")
            .GetString()
            .Should()
            .Be("Mattresses (display)");
    }

    private static JsonElement GenerateTable(FormSchemaCompileResult compiled)
    {
        using var document = JsonDocument.Parse(
            ShojiCodebookGenerator.Generate(
                compiled.FlatteningMapJson,
                compiled.CodebookJson,
                ExportFormatSettings.InterimCrunchKeySeparator));
        return document.RootElement.GetProperty("body").GetProperty("table").Clone();
    }

    private static List<string> ReadOrder(JsonElement table) =>
        table.GetProperty("order").EnumerateArray().Select(element => element.GetString()!).ToList();
}
