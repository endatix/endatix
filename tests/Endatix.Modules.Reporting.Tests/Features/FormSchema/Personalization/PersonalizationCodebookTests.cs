using System.Text.Json;
using Endatix.Core.UseCases.Submissions;
using Endatix.Modules.Audience.Contracts;
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
        block.Select(column => column.Key).Should().Equal("ParticipantIdentifier", "participant_alpha", "participant_zeta");
    }

    [Fact]
    public void Overlay_RenamedReorderedAndDeletedProperties_OrdersTheBlockAndUsesCurrentNames()
    {
        // Arrange: city sorts before age; plan and zip have no live property (deleted, or never one).
        var compiled = Personalized("age", "city", "zip", "plan");
        AudiencePropertyName[] variables =
        [
            new("age", "Age (years)", SortOrder: 2),
            new("city", "Town", SortOrder: 1),
        ];

        // Act
        var codebook = PersonalizationCodebook.Overlay(compiled.CodebookJson, compiled.FlatteningMap, variables);

        // Assert
        var block = InBlockOrder(compiled.FlatteningMap, codebook);
        block.Select(column => column.Key)
            .Should().Equal("ParticipantIdentifier", "participant_city", "participant_age", "participant_plan", "participant_zip");
        block.Select(column => column.Label)
            .Should().Equal("Participant identifier", "Town", "Age (years)", "plan", "zip");
    }

    [Fact]
    public void Overlay_PersonalizedSchema_AddsTheParticipantsBlock()
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
        var block = document.RootElement.GetProperty("participants");
        block.GetProperty("title").GetProperty("default").GetString().Should().Be("Participants");
        block.GetProperty("identifier").GetString().Should().Be("ParticipantIdentifier");
        block.GetProperty("variables").EnumerateObject().Select(variable => (variable.Name, variable.Value.GetString()))
            .Should().Equal(("city", "participant_city"), ("age", "participant_age"));
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
            .Should().Equal("Participant identifier", "Town");
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

    [Fact]
    public void Overlay_PersonalizedSchema_WritesTheParticipantsBlockOnly()
    {
        // Arrange
        var compiled = Personalized("city");

        // Act
        var codebook = PersonalizationCodebook.Overlay(compiled.CodebookJson, compiled.FlatteningMap, [new("city", "City", 0)]);

        // Assert
        using var document = JsonDocument.Parse(codebook);
        document.RootElement.TryGetProperty("participants", out _).Should().BeTrue();
        document.RootElement.TryGetProperty("personalization", out _).Should().BeFalse();
    }

    [Fact]
    public void Overlay_SchemaStillHoldingCanaryAudienceKeys_LeavesThemOutOfTheParticipantsBlock()
    {
        // Arrange: a schema flattened by a canary before the rename, not yet re-flattened.
        FormSchemaCompiler compiler = new();
        var compiled = compiler.CompilePersisted(Definition);
        var withCanaryKeys = compiler.IncludePersonalization(
            Definition,
            new PersistedSchemaJson(compiled.FlatteningMapJson, compiled.CodebookJson),
            [
                new FormSchemaColumn("AudienceIdentifier", FormSchemaColumnKind.Personalization, "Audience identifier", "text"),
                new FormSchemaColumn("audience_a", FormSchemaColumnKind.Personalization, "a", "text"),
                new FormSchemaColumn("audience_city", FormSchemaColumnKind.Personalization, "city", "text"),
            ]);

        // Act
        var codebook = PersonalizationCodebook.Overlay(
            withCanaryKeys.CodebookJson,
            withCanaryKeys.FlatteningMap,
            [new("a", "A", 0), new("city", "City", 1)]);

        // Assert
        using var document = JsonDocument.Parse(codebook);
        var block = document.RootElement.GetProperty("participants");
        block.TryGetProperty("identifier", out _).Should().BeFalse();
        block.GetProperty("variables").EnumerateObject().Should().BeEmpty();
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
