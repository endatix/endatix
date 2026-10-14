using Endatix.Core.Entities;
using Endatix.Core.UseCases.Submissions;
using Endatix.Modules.Reporting.Contracts.Export;
using Endatix.Modules.Reporting.Features.Export;
using Endatix.Modules.Reporting.Features.FormSchema.FormSchema;
using Endatix.Modules.Reporting.Features.FormSchema.Personalization;
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
    public void Build_WithCustomKeySeparator_TransformsNativeExportKeys()
    {
        string definitionJson = FormSchemaFixtureLoader.LoadAllQuestionsText("all-questions-definition.json");
        FormSchemaCompiler compiler = new();
        FormSchemaCompileResult compiled = compiler.CompilePersisted(definitionJson);
        FormSchemaEntity schema = new(1, 100, 1, compiled.FlatteningMapJson, compiled.CodebookJson);

        IExportColumnPlan plan = ExportColumnPlanBuilder.Build(schema, keySeparator: "--");

        plan.Columns.First(column => column.CanonicalKey == "qTagBox__adidas").ExportKey
            .Should().Be("qTagBox--adidas");
    }

    [Theory]
    [InlineData("--")]
    [InlineData(".")]
    public void Build_AudienceColumnsWithAnyKeySeparator_KeepTheirStoredKeysAsOneBlockLast(string keySeparator)
    {
        // Arrange
        var schema = PersonalizedSchema();

        // Act
        var plan = ExportColumnPlanBuilder.Build(schema, keySeparator: keySeparator);

        // Assert
        plan.Columns.TakeLast(3).Select(column => (column.ExportKey, column.HeaderLabel)).Should().Equal(
            ("AudienceIdentifier", "Audience identifier"),
            ("audience_last_seen", "Last seen"),
            ("audience_panel_member", "Panel member"));
    }

    [Fact]
    public void Build_WithQuestionColumnScope_KeepsTheAudienceBlock()
    {
        // Arrange
        var schema = PersonalizedSchema();
        IReadOnlySet<string> columnScope = new HashSet<string>(["qName"], StringComparer.Ordinal);

        // Act
        var plan = ExportColumnPlanBuilder.Build(schema, columnScope: columnScope);

        // Assert
        plan.Columns.Skip(SubmissionExportRow.SystemColumns.Count).Select(column => column.ExportKey).Should().Equal(
            "qName", "AudienceIdentifier", "audience_last_seen", "audience_panel_member");
    }

    private static FormSchemaEntity PersonalizedSchema()
    {
        const string definitionJson = """
            {"pages":[{"name":"p1","elements":[{"type":"text","name":"qName"},{"type":"text","name":"qCity"}]}]}
            """;
        FormSchemaCompiler compiler = new();
        var compiled = compiler.CompilePersisted(definitionJson);
        var read = PersonalizationReader.Read("""
            {"identifier":"CG-1","variables":{"panel_member":true,"last_seen":"2026-10-01T10:00:00"},
             "values":[{"variableName":"panel_member","name":"Panel member","dataType":"boolean"},
                       {"variableName":"last_seen","name":"Last seen","dataType":"date_time"}]}
            """);
        compiled = compiler.IncludePersonalization(
            definitionJson,
            new PersistedSchemaJson(compiled.FlatteningMapJson, compiled.CodebookJson),
            PersonalizationColumns.From(read));
        return new FormSchemaEntity(1, 100, 1, compiled.FlatteningMapJson, compiled.CodebookJson);
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
    public void Build_CalculatedValueNamedLikeQuestion_UsesCalculatedValueNameAsHeader()
    {
        // Arrange
        const string definitionJson = """
            {
              "pages":[{"elements":[{"type":"ranking","name":"qRanking","title":"Rank the fruit","choices":["apple","pear"]}]}],
              "calculatedValues":[{"name":"qRanking","expression":"1","includeIntoResult":true}]
            }
            """;
        FormSchemaCompileResult compiled = new FormSchemaCompiler().CompilePersisted(definitionJson);
        FormSchemaEntity schema = new(1, 100, 1, compiled.FlatteningMapJson, compiled.CodebookJson);

        // Act
        IExportColumnPlan plan = ExportColumnPlanBuilder.Build(schema, locale: "default");

        // Assert
        plan.Columns.Single(column => column.CanonicalKey == "qRanking").HeaderLabel.Should().Be("qRanking");
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
        ExportColumnDefinition? adidasColumn = plan.Columns.FirstOrDefault(column => column.CanonicalKey == "qTagBox__adidas");
        adidasColumn.Should().NotBeNull();
        adidasColumn!.HeaderLabel.Should().Be("Select Sport Brands (Adidas)");
        adidasColumn.Source.Should().Be(ExportColumnSource.DataJson);
    }

    [Fact]
    public void Build_WithEmptyKeySeparator_ThrowsArgumentException()
    {
        FormSchemaEntity schema = CreateSchemaWithCollidingCanonicalKeys();

        Action act = () => ExportColumnPlanBuilder.Build(schema, keySeparator: "");

        act.Should().Throw<ArgumentException>()
            .WithMessage("*key separator cannot be empty*");
    }

    [Fact]
    public void Build_WithCollidingExportKeys_ThrowsInvalidOperationException()
    {
        FormSchemaEntity schema = CreateSchemaWithCollidingCanonicalKeys();

        Action act = () => ExportColumnPlanBuilder.Build(schema, keySeparator: "_");

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*Duplicate export column keys*")
            .WithMessage("*q1__other*")
            .WithMessage("*q1_other*");
    }

    private static FormSchemaEntity CreateSchemaWithCollidingCanonicalKeys()
    {
        const string flatteningMapJson = """
            {
              "version": 1,
              "columns": [
                { "key": "q1__other", "kind": "Simple", "label": "One", "dataType": "string" },
                { "key": "q1_other", "kind": "Simple", "label": "Two", "dataType": "string" }
              ]
            }
            """;

        return new FormSchemaEntity(1, 100, 1, flatteningMapJson, """{"columns":{}}""");
    }
}
