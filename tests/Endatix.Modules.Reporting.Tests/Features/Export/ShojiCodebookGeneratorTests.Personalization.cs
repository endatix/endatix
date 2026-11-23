using System.Text.Json;
using Endatix.Core.Entities;
using Endatix.Core.UseCases.Submissions;
using Endatix.Modules.Reporting.Features.Export.Integrations.Crunch.Shoji;
using Endatix.Modules.Reporting.Features.FormSchema.FormSchema;
using Endatix.Modules.Reporting.Features.FormSchema.Personalization;
using FluentAssertions;

namespace Endatix.Modules.Reporting.Tests.Features.Export;

public sealed partial class ShojiCodebookGeneratorTests
{
    [Fact]
    public void Generate_DirectPrefill_KeepsTheLocalizedQuestionDescription()
    {
        const string definition = """
            {"pages":[{"elements":[{"type":"text","name":"city","defaultValueExpression":"{home}",
              "title":{"default":"City","es":"Ciudad"},
              "description":{"default":"Use your current address.","es":"Usa tu dirección actual."}}]}]}
            """;
        FormSchemaCompiler compiler = new();
        var schema = compiler.CompilePersisted(definition);
        var compiled = compiler.IncludePersonalization(definition,
            new PersistedSchemaJson(schema.FlatteningMapJson, schema.CodebookJson),
            PersonalizationColumns.From(PersonalizationReader.Read("""{"variables":{"home":"Toledo"}}""")));

        using var document = JsonDocument.Parse(ShojiCodebookGenerator.Generate(
            compiled.FlatteningMapJson, compiled.CodebookJson, locale: "es"));

        var city = document.RootElement.GetProperty("body").GetProperty("table").GetProperty("metadata").GetProperty("city");
        city.GetProperty("description").GetString().Should().Be(
            "Ciudad (prefilled from participant variable: home) Usa tu dirección actual.");
        city.GetProperty("notes").GetString().Should().Be("Prefilled from participant variable: home");
    }

    [Fact]
    public void Generate_AudiencePropertyTitledLikeAQuestion_QuestionKeepsItsNameAndOrderIsUnchanged()
    {
        // Arrange
        const string definitionJson = """
            {"pages":[{"name":"page1","elements":[{"type":"text","name":"qCity","title":"City"}]}]}
            """;
        FormSchemaCompiler compiler = new();
        var compiled = compiler.CompilePersisted(definitionJson);
        compiled = compiler.IncludePersonalization(
            definitionJson,
            new PersistedSchemaJson(compiled.FlatteningMapJson, compiled.CodebookJson),
            PersonalizationColumns.From(PersonalizationReader.Read("""
                {"identifier":"CG-1","variables":{"city":"Columbus"},
                 "values":[{"variableName":"city","name":"City","dataType":"text"}]}
                """)));

        // Act
        var table = GenerateTable(compiled);

        // Assert
        var metadata = table.GetProperty("metadata");
        metadata.GetProperty("qCity").GetProperty("name").GetString().Should().Be("City");
        metadata.GetProperty("participant_city").GetProperty("name").GetString().Should().Be("City -- participant_city");
        ReadOrder(table).Skip(OrderedSystemColumnCount)
            .Should().Equal(PersonalizationColumns.IdentifierKey, "participant_city", "qCity");
    }

    [Fact]
    public void Generate_QuestionAliasMatchesAnAudienceKey_TheQuestionKeepsItsPlaceInTheOrder()
    {
        // Arrange: with "_" as the separator, item "region" of question "audience" takes the alias "participant_region".
        const string definitionJson = """
            {"pages":[{"name":"page1","elements":[
              {"type":"text","name":"qFirst"},
              {"type":"multipletext","name":"participant","items":[{"name":"region"}]}
            ]}]}
            """;
        FormSchemaCompiler compiler = new();
        var compiled = compiler.CompilePersisted(definitionJson);
        compiled = compiler.IncludePersonalization(
            definitionJson,
            new PersistedSchemaJson(compiled.FlatteningMapJson, compiled.CodebookJson),
            PersonalizationColumns.From(PersonalizationReader.Read("""{"identifier":"CG-1","variables":{"region":"North"}}""")));

        // Act
        var table = GenerateTable(compiled, keySeparator: "_");

        // Assert
        ReadOrder(table).Skip(OrderedSystemColumnCount)
            .Should().Equal(PersonalizationColumns.IdentifierKey, "qFirst", "participant_region");
    }
}
