using System.Text.Json;
using Endatix.Modules.Reporting.Features.FormSchema.FormSchema;
using Endatix.Modules.Reporting.Features.SubmissionGrid;

namespace Endatix.Modules.Reporting.Tests.Features.SubmissionGrid;

public sealed class SubmissionGridFilterTypesTests
{
    [Theory]
    [InlineData("text", null, SubmissionGridFilterTypes.Text)]
    [InlineData("text", "email", SubmissionGridFilterTypes.Text)]
    [InlineData("text", "date", SubmissionGridFilterTypes.Date)]
    [InlineData("text", "datetime-local", SubmissionGridFilterTypes.DateTime)]
    [InlineData("text", "DateTime", SubmissionGridFilterTypes.DateTime)]
    [InlineData("comment", null, SubmissionGridFilterTypes.Text)]
    [InlineData("dropdown", null, SubmissionGridFilterTypes.Text)]
    [InlineData("radiogroup", null, SubmissionGridFilterTypes.Text)]
    public void Resolve_SimpleQuestionValue_ReturnsFilterType(string questionType, string? inputType, string expected)
    {
        // Arrange
        var column = SimpleColumn("q1");
        var question = Question(questionType, inputType);

        // Act
        var filterType = SubmissionGridFilterTypes.Resolve(column, question);

        // Assert
        filterType.Should().Be(expected);
    }

    [Theory]
    [InlineData("text", "number")]
    [InlineData("text", "color")]
    [InlineData("boolean", null)]
    [InlineData("rating", null)]
    [InlineData("checkbox", null)]
    [InlineData("tagbox", null)]
    public void Resolve_UnsupportedQuestion_ReturnsNull(string questionType, string? inputType)
    {
        // Arrange
        var column = SimpleColumn("q1");
        var question = Question(questionType, inputType);

        // Act
        var filterType = SubmissionGridFilterTypes.Resolve(column, question);

        // Assert
        filterType.Should().BeNull();
    }

    [Fact]
    public void Resolve_ColumnOfAnotherQuestion_ReturnsNull()
    {
        // Arrange
        FormSchemaColumn otherText = new("q1-Comment", FormSchemaColumnKind.Simple, "Other", "string", SourceQuestion: "q1");

        // Act
        var filterType = SubmissionGridFilterTypes.Resolve(otherText, Question("dropdown", null));

        // Assert
        filterType.Should().BeNull();
    }

    [Fact]
    public void Resolve_ChoiceIndicatorColumn_ReturnsNull()
    {
        // Arrange
        FormSchemaColumn indicator = new(
            "q1--a",
            FormSchemaColumnKind.ChoiceIndicator,
            "A",
            "boolean",
            SourceQuestion: "q1",
            ChoiceValue: "a");

        // Act
        var filterType = SubmissionGridFilterTypes.Resolve(indicator, Question("checkbox", null));

        // Assert
        filterType.Should().BeNull();
    }

    [Fact]
    public void Resolve_WithoutCodebookQuestion_ReturnsNull()
    {
        // Arrange
        var column = SimpleColumn("q1");

        // Act
        var filterType = SubmissionGridFilterTypes.Resolve(column, question: null);

        // Assert
        filterType.Should().BeNull();
    }

    private static FormSchemaColumn SimpleColumn(string key) =>
        new(key, FormSchemaColumnKind.Simple, key, "string");

    private static JsonElement Question(string surveyJsType, string? inputType)
    {
        Dictionary<string, string> entry = new() { ["surveyJsType"] = surveyJsType };
        if (inputType is not null)
        {
            entry["inputType"] = inputType;
        }

        return JsonSerializer.SerializeToElement(entry);
    }
}
