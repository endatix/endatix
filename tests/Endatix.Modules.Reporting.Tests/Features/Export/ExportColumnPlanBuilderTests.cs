using Endatix.Core.Entities;
using Endatix.Modules.Reporting.Contracts.Export;
using Endatix.Modules.Reporting.Features.Export;
using Endatix.Modules.Reporting.Features.FormSchema.FormSchema;
using Endatix.Modules.Reporting.Tests.Features.FormSchema.FormSchema;
using FormSchemaEntity = Endatix.Modules.Reporting.Domain.FormSchema;

namespace Endatix.Modules.Reporting.Tests.Features.Export;

public sealed class ExportColumnPlanBuilderTests
{
    [Fact]
    public void Build_WithAllQuestionsSchema_PrependsSystemColumnsAndPreservesFlatteningOrder()
    {
        // Arrange
        string definitionJson = FormSchemaFixtureLoader.LoadAllQuestionsText("all-questions-definition.json");
        IReadOnlyList<string> expectedKeys = FormSchemaFixtureLoader.LoadAllQuestionsExpectedKeys();
        FormSchemaCompiler compiler = new();
        FormSchemaCompileResult compiled = compiler.CompilePersisted(definitionJson);
        FormSchemaEntity schema = new(1, 100, 1, compiled.FlatteningMapJson, compiled.CodebookJson);

        // Act
        IExportColumnPlan plan = ExportColumnPlanBuilder.Build(schema);

        // Assert
        List<ExportColumnDefinition> columns = plan.Columns.ToList();
        columns.Take(SubmissionExportRow.SystemColumns.Count).Select(column => column.CanonicalKey).Should().BeEquivalentTo(
            SubmissionExportRow.SystemColumns.OrderedKeys,
            options => options.WithStrictOrdering());

        columns.Skip(SubmissionExportRow.SystemColumns.Count).Select(column => column.CanonicalKey).Should().BeEquivalentTo(
            expectedKeys,
            options => options.WithStrictOrdering());

        columns.Take(SubmissionExportRow.SystemColumns.Count).Should().AllSatisfy(column =>
        {
            column.ExportKey.Should().Be(column.CanonicalKey);
            column.Source.Should().Be(ExportColumnSource.System);
        });

        columns.Skip(SubmissionExportRow.SystemColumns.Count).Should().AllSatisfy(column =>
        {
            column.ExportKey.Should().Be(column.CanonicalKey);
            column.Source.Should().Be(ExportColumnSource.DataJson);
        });
    }

    [Fact]
    public void Build_WithAllQuestionsSchema_CrunchProfile_ProducesExpectedExportKeys()
    {
        // Arrange
        string definitionJson = FormSchemaFixtureLoader.LoadAllQuestionsText("all-questions-definition.json");
        IReadOnlyDictionary<string, string> expectedExportKeys = FormSchemaFixtureLoader.LoadAllQuestionsExpectedCrunchExportKeys();
        FormSchemaCompiler compiler = new();
        FormSchemaCompileResult compiled = compiler.CompilePersisted(definitionJson);
        FormSchemaEntity schema = new(1, 100, 1, compiled.FlatteningMapJson, compiled.CodebookJson);

        // Act
        IExportColumnPlan plan = ExportColumnPlanBuilder.Build(schema, aliasProfile: ColumnAliasProfile.Crunch);

        // Assert
        Dictionary<string, string> actualExportKeys = plan.Columns.ToDictionary(
            column => column.CanonicalKey,
            column => column.ExportKey,
            StringComparer.Ordinal);

        actualExportKeys.Should().BeEquivalentTo(expectedExportKeys);
    }

    [Fact]
    public void Build_WithColumnScope_FiltersDataColumnsButKeepsSystemColumns()
    {
        string definitionJson = FormSchemaFixtureLoader.LoadAllQuestionsText("all-questions-definition.json");
        IReadOnlyList<string> expectedKeys = FormSchemaFixtureLoader.LoadAllQuestionsExpectedKeys();
        FormSchemaCompiler compiler = new();
        FormSchemaCompileResult compiled = compiler.CompilePersisted(definitionJson);
        FormSchemaEntity schema = new(1, 100, 1, compiled.FlatteningMapJson, compiled.CodebookJson);
        string[] expectedScopedKeys = [expectedKeys[0], expectedKeys[1]];
        IReadOnlySet<string> columnScope = new HashSet<string>(expectedScopedKeys, StringComparer.Ordinal);

        IExportColumnPlan plan = ExportColumnPlanBuilder.Build(schema, columnScope: columnScope);

        List<ExportColumnDefinition> columns = plan.Columns.ToList();
        columns.Take(SubmissionExportRow.SystemColumns.Count).Select(column => column.CanonicalKey).Should().BeEquivalentTo(
            SubmissionExportRow.SystemColumns.OrderedKeys,
            options => options.WithStrictOrdering());
        columns.Skip(SubmissionExportRow.SystemColumns.Count).Select(column => column.CanonicalKey).Should().BeEquivalentTo(
            expectedScopedKeys,
            options => options.WithStrictOrdering());
    }

    [Fact]
    public void Build_WithAllQuestionsSchema_ResolvesLocalizedHeaderForChoiceColumn()
    {
        // Arrange
        string definitionJson = FormSchemaFixtureLoader.LoadAllQuestionsText("all-questions-definition.json");
        FormSchemaCompiler compiler = new();
        FormSchemaCompileResult compiled = compiler.CompilePersisted(definitionJson);
        FormSchemaEntity schema = new(1, 100, 1, compiled.FlatteningMapJson, compiled.CodebookJson);

        // Act
        IExportColumnPlan plan = ExportColumnPlanBuilder.Build(schema, locale: "default");

        // Assert
        ExportColumnDefinition? axeColumn = plan.Columns.FirstOrDefault(column => column.CanonicalKey == "qDropdown__axe");
        axeColumn.Should().NotBeNull();
        axeColumn!.HeaderLabel.Should().Be("Pick your primary weapon (Axe)");
        axeColumn.Source.Should().Be(ExportColumnSource.DataJson);
    }
}
