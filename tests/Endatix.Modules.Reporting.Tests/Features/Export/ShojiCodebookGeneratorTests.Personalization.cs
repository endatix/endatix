using Endatix.Core.Entities;
using Endatix.Core.UseCases.Submissions;
using Endatix.Modules.Reporting.Features.FormSchema.FormSchema;
using Endatix.Modules.Reporting.Features.FormSchema.Personalization;
using FluentAssertions;

namespace Endatix.Modules.Reporting.Tests.Features.Export;

public sealed partial class ShojiCodebookGeneratorTests
{
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
