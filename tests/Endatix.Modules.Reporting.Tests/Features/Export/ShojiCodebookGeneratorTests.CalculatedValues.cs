using System.Text.Json;
using Endatix.Core.Entities;
using Endatix.Modules.Reporting.Contracts.Export;
using Endatix.Modules.Reporting.Features.Export.Integrations.Crunch.Shoji;
using Endatix.Modules.Reporting.Features.FormSchema.FormSchema;
using FluentAssertions;

namespace Endatix.Modules.Reporting.Tests.Features.Export;

public sealed partial class ShojiCodebookGeneratorTests
{
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
    public void Generate_CalculatedValueNamedLikeDynamicPanelQuestion_WritesCalculatedVariable()
    {
        // Arrange
        const string definitionJson = """
            {
              "pages":[{"name":"page1","elements":[
                {"type":"paneldynamic","name":"qLoop","templateElements":[{"type":"dropdown","name":"qInner","choices":["a","b"]}]}
              ]}],
              "calculatedValues":[{"name":"qInner","expression":"1","includeIntoResult":true}]
            }
            """;
        var compiled = new FormSchemaCompiler().CompilePersisted(definitionJson);

        // Act
        var table = GenerateTable(compiled);
        var metadata = table.GetProperty("metadata");
        var order = ReadOrder(table);

        // Assert
        metadata.GetProperty("qInner").GetProperty("type").GetString().Should().Be("text");
        metadata.GetProperty("qInner").GetProperty("name").GetString().Should().Be("qInner");
        order[SubmissionExportRow.SystemColumns.Count].Should().Be("qInner");
        order.Should().Contain("qLoop--0--qInner");
    }

    [Fact]
    public void Generate_QuestionTookOverCalculatedValueName_WritesQuestionInSurveyOrder()
    {
        // Arrange
        const string v1 = """
            {
              "pages":[{"name":"page1","elements":[{"type":"text","name":"qName"}]}],
              "calculatedValues":[{"name":"score","expression":"1","includeIntoResult":true}]
            }
            """;
        const string v2 = """
            {"pages":[{"name":"page1","elements":[{"type":"text","name":"qName"},{"type":"text","name":"score","title":"Your score"}]}]}
            """;
        FormSchemaCompiler compiler = new();
        var initial = compiler.CompilePersisted(v1);
        var merged = compiler.CompilePersisted(v2, initial.FlatteningMapJson, initial.CodebookJson, FormSchemaCompileMode.Merge);

        // Act
        var table = GenerateTable(merged);
        var order = ReadOrder(table);

        // Assert
        table.GetProperty("metadata").GetProperty("score").GetProperty("name").GetString().Should().Be("Your score");
        order.Skip(SubmissionExportRow.SystemColumns.Count).Should().Equal("qName", "score");
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
