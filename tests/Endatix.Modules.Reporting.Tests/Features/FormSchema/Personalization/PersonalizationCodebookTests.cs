using System.Text.Json;
using Endatix.Core.Abstractions.Submissions;
using Endatix.Core.UseCases.Submissions;
using Endatix.Modules.Reporting.Features.FormSchema.FormSchema;
using Endatix.Modules.Reporting.Features.FormSchema.Personalization;
using FluentAssertions;

namespace Endatix.Modules.Reporting.Tests.Features.FormSchema.Personalization;

public sealed class PersonalizationCodebookTests
{
    private const string Definition = """{"pages":[{"name":"p1","elements":[{"type":"text","name":"q1"}]}]}""";

    [Fact]
    public void InBlockOrder_CodebookWithoutBlock_ReturnsIdentifierThenVariablesByKey()
    {
        // Arrange
        var compiled = Personalized("zeta", "alpha");

        // Act
        var block = InBlockOrder(compiled.FlatteningMap, compiled.CodebookJson);

        // Assert
        block.Select(column => column.Key).Should().Equal("AudienceIdentifier", "audience_alpha", "audience_zeta");
    }

    [Fact]
    public void Overlay_RenamedReorderedAndDeletedProperties_OrdersTheBlockAndUsesCurrentNames()
    {
        // Arrange: city sorts before age; plan and zip have no live property (deleted, or never one).
        var compiled = Personalized("age", "city", "zip", "plan");
        PersonalizationVariableInfo[] variables =
        [
            new("age", "Age (years)", SortOrder: 2),
            new("city", "Town", SortOrder: 1),
        ];

        // Act
        var codebook = PersonalizationCodebook.Overlay(compiled.CodebookJson, compiled.FlatteningMap, variables);

        // Assert
        var block = InBlockOrder(compiled.FlatteningMap, codebook);
        block.Select(column => column.Key)
            .Should().Equal("AudienceIdentifier", "audience_city", "audience_age", "audience_plan", "audience_zip");
        block.Select(column => column.Label)
            .Should().Equal("Audience identifier", "Town", "Age (years)", "plan", "zip");
    }

    [Fact]
    public void Overlay_PersonalizedSchema_AddsThePersonalizationBlock()
    {
        // Arrange
        var compiled = Personalized("city", "age");

        // Act
        var codebook = PersonalizationCodebook.Overlay(
            compiled.CodebookJson,
            compiled.FlatteningMap,
            [new("city", "City", 0), new("age", "Age", 1)]);

        // Assert
        using var document = JsonDocument.Parse(codebook);
        var block = document.RootElement.GetProperty("personalization");
        block.GetProperty("title").GetProperty("default").GetString().Should().Be("Personalized");
        block.GetProperty("identifier").GetString().Should().Be("AudienceIdentifier");
        block.GetProperty("variables").EnumerateObject().Select(variable => (variable.Name, variable.Value.GetString()))
            .Should().Equal(("city", "audience_city"), ("age", "audience_age"));
    }

    [Fact]
    public void Overlay_NoProperties_ReturnsTheCodebookUnchanged()
    {
        // Arrange: the Audience module is off, so the catalog knows no properties.
        var compiled = Personalized("city");

        // Act
        var codebook = PersonalizationCodebook.Overlay(compiled.CodebookJson, compiled.FlatteningMap, []);

        // Assert
        codebook.Should().BeSameAs(compiled.CodebookJson);
    }

    [Fact]
    public void Overlay_DuplicateVariableNames_UsesTheFirstInsteadOfFailing()
    {
        // Arrange
        var compiled = Personalized("city");

        // Act
        var codebook = PersonalizationCodebook.Overlay(
            compiled.CodebookJson,
            compiled.FlatteningMap,
            [new("city", "Town", 0), new("city", "City", 1)]);

        // Assert
        InBlockOrder(compiled.FlatteningMap, codebook).Select(column => column.Label)
            .Should().Equal("Audience identifier", "Town");
    }

    [Fact]
    public void Overlay_SchemaWithoutAudienceColumns_ReturnsTheCodebookUnchanged()
    {
        // Arrange
        var compiled = new FormSchemaCompiler().CompilePersisted(Definition);

        // Act
        var codebook = PersonalizationCodebook.Overlay(compiled.CodebookJson, compiled.FlatteningMap, []);

        // Assert
        codebook.Should().BeSameAs(compiled.CodebookJson);
    }

    private static IReadOnlyList<FormSchemaColumn> InBlockOrder(MergedFormSchema flatteningMap, string codebookJson)
    {
        using var codebook = JsonDocument.Parse(codebookJson);
        return PersonalizationCodebook.InBlockOrder(flatteningMap, codebook.RootElement);
    }

    // Variables frozen without values[] are labelled by their variable name.
    private static FormSchemaCompileResult Personalized(params string[] variableNames)
    {
        FormSchemaCompiler compiler = new();
        var compiled = compiler.CompilePersisted(Definition);
        var variables = string.Join(",", variableNames.Select(name => $"\"{name}\":\"x\""));
        return compiler.IncludePersonalization(
            Definition,
            new PersistedSchemaJson(compiled.FlatteningMapJson, compiled.CodebookJson),
            PersonalizationColumns.From(PersonalizationReader.Read("""{"identifier":"CG-1","variables":{""" + variables + "}}")));
    }
}
