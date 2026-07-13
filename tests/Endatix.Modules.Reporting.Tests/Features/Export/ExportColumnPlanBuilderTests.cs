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
        columns.Take(8).Select(column => column.CanonicalKey).Should().BeEquivalentTo(
        [
            "FormId",
            "Id",
            "IsComplete",
            "CreatedAt",
            "ModifiedAt",
            "CompletedAt",
            "SubmitterId",
            "SubmitterDisplayId",
        ],
            options => options.WithStrictOrdering());

        columns.Skip(8).Select(column => column.CanonicalKey).Should().BeEquivalentTo(
            expectedKeys,
            options => options.WithStrictOrdering());

        columns.Should().AllSatisfy(column =>
        {
            column.ExportKey.Should().Be(column.CanonicalKey);
            column.Source.Should().BeOneOf(ExportColumnSource.System, ExportColumnSource.DataJson);
        });
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
