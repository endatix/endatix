using Endatix.Modules.Reporting.Features.FormSchema.FormSchema;
using Endatix.Modules.Reporting.Features.SearchCopy;
using Endatix.Modules.Reporting.Features.SubmissionGrid;

namespace Endatix.Modules.Reporting.Tests.Features.SearchCopy;

public sealed class SearchableColumnsTests
{
    [Fact]
    public void CompilePersisted_FilterableQuestions_AreSearchableInSchemaOrder()
    {
        // Arrange
        const string definition = """
            { "pages": [ { "elements": [
              { "type": "text", "name": "company" },
              { "type": "checkbox", "name": "colors", "choices": ["red", "blue"] },
              { "type": "text", "name": "visit", "inputType": "datetime-local" },
              { "type": "text", "name": "age", "inputType": "number" },
              { "type": "comment", "name": "notes" },
              { "type": "text", "name": "born", "inputType": "date" }
            ] } ] }
            """;

        // Act
        var searchable = new FormSchemaCompiler().CompilePersisted(definition).SearchableColumns;

        // Assert
        searchable.Should().Equal(
            new SearchableColumn("company", SubmissionGridFilterTypes.Text),
            new SearchableColumn("visit", SubmissionGridFilterTypes.DateTime),
            new SearchableColumn("notes", SubmissionGridFilterTypes.Text),
            new SearchableColumn("born", SubmissionGridFilterTypes.Date));
    }

    [Fact]
    public void CompilePersisted_ReadOnlyQuestion_IsNotSearchable()
    {
        // Arrange
        const string definition = """
            { "pages": [ { "elements": [
              { "type": "text", "name": "company" },
              { "type": "text", "name": "reference", "readOnly": true },
              { "type": "panel", "name": "locked", "readOnly": true, "elements": [
                { "type": "text", "name": "inLockedPanel" }
              ] }
            ] }, { "name": "lockedPage", "readOnly": true, "elements": [
              { "type": "comment", "name": "onLockedPage" }
            ] } ] }
            """;

        // Act
        var searchable = new FormSchemaCompiler().CompilePersisted(definition).SearchableColumns;

        // Assert
        searchable.Select(column => column.Key).Should().Equal("company");
    }
}
