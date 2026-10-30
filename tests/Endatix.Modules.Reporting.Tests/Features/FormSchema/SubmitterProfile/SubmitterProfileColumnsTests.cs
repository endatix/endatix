using System.Text.Json;
using Endatix.Modules.Reporting.Features.Export;
using Endatix.Modules.Reporting.Features.FlattenedSubmission;
using Endatix.Modules.Reporting.Features.FormSchema.FormSchema;
using Endatix.Modules.Reporting.Features.FormSchema.SubmitterProfile;
using Endatix.Modules.Reporting.Tests.Features.FormSchema.FormSchema;
using FluentAssertions;
using FormSchemaEntity = Endatix.Modules.Reporting.Domain.FormSchema;

namespace Endatix.Modules.Reporting.Tests.Features.FormSchema.SubmitterProfile;

public sealed class SubmitterProfileColumnsTests
{
    private static readonly string _definition = FormSchemaFixtureLoader.LoadText("simple-definition.json");

    [Fact]
    public void CompilePersisted_EmptyAllowlist_CompilesAsWithoutSubmitterFields()
    {
        // Arrange
        FormSchemaCompiler withEmptyAllowlist = new(submitterProfileFields: new SubmitterProfileFields([]));

        // Act
        var compiled = withEmptyAllowlist.CompilePersisted(_definition);

        // Assert
        var unconfigured = new FormSchemaCompiler().CompilePersisted(_definition);
        compiled.FlatteningMapJson.Should().Be(unconfigured.FlatteningMapJson);
        compiled.CodebookJson.Should().Be(unconfigured.CodebookJson);
        compiled.FlatteningMap.Columns.Should().NotContain(column => column.Kind == FormSchemaColumnKind.SubmitterProfile);
    }

    [Fact]
    public void CompilePersisted_ListedFields_CompilesOnlyTheDefinitionColumns()
    {
        // Arrange
        FormSchemaCompiler compiler = new(submitterProfileFields: new SubmitterProfileFields(["email"]));

        // Act
        var compiled = compiler.CompilePersisted(_definition);

        // Assert
        compiled.FlatteningMapJson.Should().Be(new FormSchemaCompiler().CompilePersisted(_definition).FlatteningMapJson);
    }

    [Fact]
    public void HasColumnsFor_SchemaCompiledBeforeTheFieldWasListed_IsTrue()
    {
        // Arrange
        var compiledBefore = new FormSchemaCompiler().CompilePersisted(_definition);
        FormSchemaCompiler compiler = new(submitterProfileFields: new SubmitterProfileFields(["email"]));

        // Act
        var hasColumns = compiler.HasColumnsFor(compiledBefore.FlatteningMapJson, _definition);

        // Assert
        hasColumns.Should().BeTrue("a listed field must not make a definition look uncompiled and rebuild from it");
    }

    [Fact]
    public void Columns_ListedFields_OneStringColumnPerFieldWithItsLabel()
    {
        // Arrange & Act
        SubmitterProfileFields fields = new(["email", "department"]);

        // Assert
        fields.Columns.Should().BeEquivalentTo(
            [
                new FormSchemaColumn("submitter_email", FormSchemaColumnKind.SubmitterProfile, "Submitter email", "string"),
                new FormSchemaColumn("submitter_department", FormSchemaColumnKind.SubmitterProfile, "Submitter department", "string"),
            ],
            options => options.WithStrictOrdering());
    }

    [Fact]
    public void Columns_BlankAndRepeatedFields_OneColumnPerFieldInConfiguredOrder()
    {
        // Arrange & Act
        SubmitterProfileFields fields = new(["email", " ", "department", "email", " region "]);

        // Assert
        fields.Columns.Select(column => column.Key).Should().Equal("submitter_email", "submitter_department", "submitter_region");
    }

    [Fact]
    public void Read_SnapshotWithUnlistedFields_KeepsOnlyListedTextFields()
    {
        // Arrange
        SubmitterProfileFields fields = new(["email", "age"]);

        // Act
        var profile = fields.Read("""{"email":"ana@example.com","phone":"+1 555 0100","age":42}""");

        // Assert
        profile.Should().BeEquivalentTo(new Dictionary<string, string> { ["email"] = "ana@example.com" });
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("not json")]
    [InlineData("""["email"]""")]
    [InlineData("""{"phone":"+1 555 0100"}""")]
    public void Read_SnapshotWithoutListedFields_ReturnsNull(string? snapshot)
    {
        // Arrange
        SubmitterProfileFields fields = new(["email"]);

        // Act
        var profile = fields.Read(snapshot);

        // Assert
        profile.Should().BeNull();
    }

    [Fact]
    public void Read_EmptyAllowlist_ReturnsNull()
    {
        // Act
        var profile = SubmitterProfileFields.None.Read("""{"email":"ana@example.com"}""");

        // Assert
        profile.Should().BeNull();
    }

    [Fact]
    public void Flatten_SchemaColumnOfAFieldNoLongerListed_IsNull()
    {
        // Arrange
        var schema = WithSubmitterColumns(["email"]).FlatteningMap;
        ResponseSnapshots snapshots = new(SubmitterProfile: SubmitterProfileFields.None.Read("""{"email":"ana@example.com"}"""));
        using var answers = JsonDocument.Parse("{}");

        // Act
        var flattened = FlattenedSubmissionFlattener.Flatten(answers.RootElement, schema, snapshots);

        // Assert
        flattened.Should().ContainKey("submitter_email").WhoseValue.Should().BeNull();
    }

    [Fact]
    public void ExportColumnPlan_ListedFields_CloseTheRowAsTextColumnsWithTheirLabels()
    {
        // Arrange
        var compiled = WithSubmitterColumns(["email"]);
        FormSchemaEntity schema = new(1, 100, 1, compiled.FlatteningMapJson, compiled.CodebookJson);

        // Act
        var last = ExportColumnPlanBuilder.Build(schema).Columns[^1];

        // Assert
        last.ExportKey.Should().Be("submitter_email");
        last.HeaderLabel.Should().Be("Submitter email");
        last.DataType.Should().Be("string");
    }

    // The schema as a flatten's include leaves it: the definition's columns, then the listed submitter columns.
    private static FormSchemaCompileResult WithSubmitterColumns(IEnumerable<string> fields)
    {
        FormSchemaCompiler compiler = new(submitterProfileFields: new SubmitterProfileFields(fields));
        var compiled = compiler.CompilePersisted(_definition);
        return compiler.IncludePersonalization(
            _definition,
            new PersistedSchemaJson(compiled.FlatteningMapJson, compiled.CodebookJson),
            compiler.SubmitterProfileColumns);
    }
}
