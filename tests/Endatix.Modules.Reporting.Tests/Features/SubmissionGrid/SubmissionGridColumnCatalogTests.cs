using Endatix.Modules.Reporting.Features.FormSchema.FormSchema;
using Endatix.Modules.Reporting.Features.SubmissionGrid;
using Endatix.Modules.Reporting.Tests.Features.FormSchema.FormSchema;
using FormSchemaEntity = Endatix.Modules.Reporting.Domain.FormSchema;

namespace Endatix.Modules.Reporting.Tests.Features.SubmissionGrid;

/// <summary>
/// Grid columns of the all-questions sample, read back from its compiled schema.
/// </summary>
public sealed class SubmissionGridColumnCatalogTests
{
    [Theory]
    [InlineData("qText", SubmissionGridFilterTypes.Text)]
    [InlineData("qComment", SubmissionGridFilterTypes.Text)]
    [InlineData("qDropdown", SubmissionGridFilterTypes.Text)]
    [InlineData("qRadioGroup", SubmissionGridFilterTypes.Text)]
    [InlineData("qPanelText", SubmissionGridFilterTypes.Text)]
    [InlineData("qTextDate", SubmissionGridFilterTypes.Date)]
    [InlineData("qTextNumber", null)]
    [InlineData("qBoolean", null)]
    [InlineData("qRating", null)]
    public void Build_AllQuestionsSchema_SetsFilterTypePerQuestion(string key, string? expected)
    {
        // Arrange
        var schema = CompileAllQuestions();

        // Act
        var columns = SubmissionGridColumnCatalog.Build(schema, "default");

        // Assert
        columns.Single(column => column.Key == key).FilterType.Should().Be(expected);
    }

    [Fact]
    public void Build_AllQuestionsSchema_KeepsSchemaOrder()
    {
        // Arrange
        var schema = CompileAllQuestions();
        var expectedKeys = FormSchemaFixtureLoader.LoadAllQuestionsExpectedKeys();

        // Act
        var columns = SubmissionGridColumnCatalog.Build(schema, "default");

        // Assert
        columns.Select(column => column.Key).Should().Equal(expectedKeys);
    }

    [Fact]
    public void Build_ChoiceIndicatorColumns_AreNotFilterable()
    {
        // Arrange
        var schema = CompileAllQuestions();

        // Act
        var columns = SubmissionGridColumnCatalog.Build(schema, "default");

        // Assert
        var indicators = columns.Where(column => column.Kind == nameof(FormSchemaColumnKind.ChoiceIndicator)).ToList();
        indicators.Should().NotBeEmpty();
        indicators.Should().OnlyContain(column => column.FilterType == null);
    }

    [Theory]
    [InlineData("es", "¿Acepta los términos?")]
    [InlineData("default", "Do you agree to the terms?")]
    [InlineData("fr", "Do you agree to the terms?")]
    public void Build_WithLocale_ReturnsTitleInLocaleOrDefault(string locale, string expectedTitle)
    {
        // Arrange
        var schema = CompileAllQuestions();

        // Act
        var columns = SubmissionGridColumnCatalog.Build(schema, locale);

        // Assert
        columns.Single(column => column.Key == "qBoolean").Title.Should().Be(expectedTitle);
    }

    [Fact]
    public void Build_QuestionInPanel_ReturnsPageAndPanel()
    {
        // Arrange
        var schema = CompileAllQuestions();

        // Act
        var columns = SubmissionGridColumnCatalog.Build(schema, "default");

        // Assert
        var panelText = columns.Single(column => column.Key == "qPanelText");
        panelText.QuestionType.Should().Be("text");
        panelText.PageName.Should().Be("page1");
        panelText.PanelTitle.Should().Be("Internal Panel Group");
    }

    [Fact]
    public void Build_PageWithoutTitle_ReturnsPageNameAndNoPageTitle()
    {
        // Arrange
        var schema = CompileAllQuestions();

        // Act
        var columns = SubmissionGridColumnCatalog.Build(schema, "default");

        // Assert
        var text = columns.Single(column => column.Key == "qText");
        text.PageName.Should().Be("page1");
        text.PageTitle.Should().BeNull();
    }

    [Fact]
    public void Build_PageWithTitle_ReturnsPageTitle()
    {
        // Arrange
        var schema = Compile("""
            {
              "pages": [
                { "name": "contact", "title": "Contact details", "elements": [ { "type": "text", "name": "email" } ] }
              ]
            }
            """);

        // Act
        var columns = SubmissionGridColumnCatalog.Build(schema, "default");

        // Assert
        var email = columns.Single(column => column.Key == "email");
        email.PageName.Should().Be("contact");
        email.PageTitle.Should().Be("Contact details");
    }

    [Theory]
    [InlineData("es", "Datos de contacto")]
    [InlineData("fr", "Contact details")]
    public void Build_PageWithLocalizedTitle_ReturnsPageTitleInLocaleOrDefault(string locale, string expectedTitle)
    {
        // Arrange
        var schema = Compile("""
            {
              "pages": [
                {
                  "name": "contact",
                  "title": { "default": "Contact details", "es": "Datos de contacto" },
                  "elements": [ { "type": "text", "name": "email" } ]
                }
              ]
            }
            """);

        // Act
        var columns = SubmissionGridColumnCatalog.Build(schema, locale);

        // Assert
        columns.Single(column => column.Key == "email").PageTitle.Should().Be(expectedTitle);
    }

    private static FormSchemaEntity CompileAllQuestions() =>
        Compile(FormSchemaFixtureLoader.LoadAllQuestionsText("all-questions-definition.json"));

    private static FormSchemaEntity Compile(string definitionJson)
    {
        var compiled = new FormSchemaCompiler().CompilePersisted(definitionJson);
        return new FormSchemaEntity(1, 100, 1, compiled.FlatteningMapJson, compiled.CodebookJson);
    }
}
