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
        metadata.GetProperty("audience_city").GetProperty("name").GetString().Should().Be("City -- audience_city");
        ReadOrder(table).Skip(SubmissionExportRow.SystemColumns.Count)
            .Should().Equal(PersonalizationColumns.IdentifierKey, "audience_city", "qCity");
    }
}
