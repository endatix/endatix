using Endatix.Core.UseCases.Submissions;
using Endatix.Modules.Reporting.Features.FormSchema.FormSchema;
using Endatix.Modules.Reporting.Features.FormSchema.Personalization;
using FluentAssertions;

namespace Endatix.Modules.Reporting.Tests.Features.FormSchema.Personalization;

public sealed class PersonalizationColumnsTests
{
    private const string Snapshot = """
        {"identifier":"CG-1","variables":{"city":"Columbus","age":65},
         "values":[{"variableName":"city","name":"City","dataType":"text"},{"variableName":"age","name":"Age","dataType":"number"}]}
        """;

    [Fact]
    public void From_NoSnapshot_ReturnsNoColumns()
    {
        // Act
        var columns = PersonalizationColumns.From(null);

        // Assert
        columns.Should().BeEmpty();
    }

    [Fact]
    public void From_Snapshot_ReturnsIdentifierThenPrefixedVariablesByName()
    {
        // Act
        var columns = PersonalizationColumns.From(PersonalizationReader.Read(Snapshot));

        // Assert
        columns.Select(column => (column.Key, column.Label, column.DataType)).Should().Equal(
            ("ParticipantIdentifier", "Participant identifier", "text"),
            ("participant_age", "Age", "number"),
            ("participant_city", "City", "text"));
        columns.Should().OnlyContain(column => column.Kind == FormSchemaColumnKind.Personalization);
    }

    [Theory]
    [InlineData("12.5", "number")]
    [InlineData("true", "boolean")]
    [InlineData("""["a","b"]""", "multiple_choice")]
    [InlineData("\"2024-03-14\"", "text")]
    public void From_SnapshotWithoutValues_InfersTheTypeFromTheValue(string value, string expectedType)
    {
        // Arrange
        var read = PersonalizationReader.Read("""{"identifier":"CG-1","variables":{"score":""" + value + "}}");

        // Act
        var column = PersonalizationColumns.From(read).Single(column => column.Key == "participant_score");

        // Assert
        column.DataType.Should().Be(expectedType);
        column.Label.Should().Be("score");
    }

    [Fact]
    public void ValueOf_EachColumn_ReadsTheFrozenValue()
    {
        // Arrange
        var read = PersonalizationReader.Read(Snapshot);
        var columns = PersonalizationColumns.From(read);

        // Act
        var values = columns.ToDictionary(column => column.Key, column => PersonalizationColumns.ValueOf(column, read)?.GetRawText());

        // Assert
        values.Should().BeEquivalentTo(new Dictionary<string, string?>
        {
            ["ParticipantIdentifier"] = "\"CG-1\"",
            ["participant_age"] = "65",
            ["participant_city"] = "\"Columbus\"",
        });
    }

    [Fact]
    public void ValueOf_ResponseWithoutTheVariableOrWithoutASnapshot_IsNull()
    {
        // Arrange
        var column = PersonalizationColumns.From(PersonalizationReader.Read(Snapshot)).Single(column => column.Key == "participant_city");
        var other = PersonalizationReader.Read("""{"identifier":"CG-2","variables":{"age":40}}""");

        // Act & Assert
        PersonalizationColumns.ValueOf(column, other).Should().BeNull();
        PersonalizationColumns.ValueOf(column, null).Should().BeNull();
    }

    [Theory]
    [InlineData("AudienceIdentifier")]
    [InlineData("audience_city")]
    [InlineData("audience_a")]
    public void ValueOf_ColumnKeyedBeforeTheParticipantRename_IsNull(string canaryKey)
    {
        // Arrange: a schema flattened by a canary keeps these keys until the form is re-flattened.
        var read = PersonalizationReader.Read("""{"identifier":"CG-1","variables":{"city":"Columbus","a":"x","y":"z"}}""");
        FormSchemaColumn column = new(canaryKey, FormSchemaColumnKind.Personalization, canaryKey, "text");

        // Act
        var value = PersonalizationColumns.ValueOf(column, read);

        // Assert
        value.Should().BeNull();
    }

    [Fact]
    public void IncludeIn_NewVariables_AppendsOnlyTheMissingColumns()
    {
        // Arrange
        var schema = PersonalizationColumns.IncludeIn(Questions("city"), PersonalizationColumns.From(PersonalizationReader.Read(Snapshot)));
        var later = PersonalizationColumns.From(PersonalizationReader.Read("""{"identifier":"CG-2","variables":{"city":"Austin","joined":"2024-03-14"}}"""));

        // Act
        var included = PersonalizationColumns.IncludeIn(schema, later);

        // Assert
        included.Columns.Select(column => column.Key).Should().Equal(
            "city", "ParticipantIdentifier", "participant_age", "participant_city", "participant_joined");
    }

    [Theory]
    [InlineData("number", "text", "text")]
    [InlineData("text", "number", "text")]
    [InlineData("number", "number", "number")]
    public void IncludeIn_VariableFrozenWithAnotherType_EndsAsTextWhateverTheOrder(string firstType, string laterType, string expectedType)
    {
        // Arrange
        var first = PersonalizationColumns.IncludeIn(Questions(), [Column("age", firstType)]);

        // Act
        var included = PersonalizationColumns.IncludeIn(first, [Column("age", laterType)]);

        // Assert
        included.Columns.Single().DataType.Should().Be(expectedType);
    }

    [Fact]
    public void AreCoveredBy_SchemaWithTheColumnsAndTypes_IsTrue()
    {
        // Arrange
        var columns = PersonalizationColumns.From(PersonalizationReader.Read(Snapshot));
        var schema = PersonalizationColumns.IncludeIn(Questions(), columns);

        // Act & Assert
        PersonalizationColumns.AreCoveredBy(schema, columns).Should().BeTrue();
        PersonalizationColumns.AreCoveredBy(schema, [Column("age", "boolean")]).Should().BeFalse();
        PersonalizationColumns.AreCoveredBy(schema, [Column("joined", "date")]).Should().BeFalse();
    }

    [Fact]
    public void AreCoveredBy_QuestionAlreadyUsingTheKey_KeepsTheQuestion()
    {
        // Arrange: the first column a key had wins, as for every column.
        var schema = Questions("participant_city");

        // Act
        var covered = PersonalizationColumns.AreCoveredBy(schema, [Column("city", "text")]);

        // Assert
        covered.Should().BeTrue();
        PersonalizationColumns.IncludeIn(schema, [Column("city", "text")]).Columns.Single().Kind
            .Should().Be(FormSchemaColumnKind.Simple);
    }

    private static MergedFormSchema Questions(params string[] keys) =>
        new(keys.Select(key => new FormSchemaColumn(key, FormSchemaColumnKind.Simple, key, "string")));

    private static FormSchemaColumn Column(string variableName, string dataType) =>
        new(PersonalizationColumns.KeyOf(variableName), FormSchemaColumnKind.Personalization, variableName, dataType);
}
