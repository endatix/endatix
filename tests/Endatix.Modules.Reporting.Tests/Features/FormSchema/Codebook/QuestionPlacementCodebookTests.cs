using System.Text.Json;
using Endatix.Modules.Reporting.Features.FormSchema.FormSchema;

namespace Endatix.Modules.Reporting.Tests.Features.FormSchema.Codebook;

/// <summary>
/// The page and panel a compiled codebook stores on each question entry.
/// </summary>
public sealed class QuestionPlacementCodebookTests
{
    [Fact]
    public void CompilePersisted_LocalizedPageTitle_StoresEveryLocale()
    {
        // Arrange
        const string definition = """
            {
              "pages": [
                {
                  "name": "contact",
                  "title": { "default": "Contact details", "es": "Datos de contacto" },
                  "elements": [ { "type": "text", "name": "email" } ]
                }
              ]
            }
            """;

        // Act
        var compiled = new FormSchemaCompiler().CompilePersisted(definition);

        // Assert
        using var codebook = JsonDocument.Parse(compiled.CodebookJson);
        var pageTitle = codebook.RootElement.GetProperty("questions").GetProperty("email").GetProperty("pageTitle");
        pageTitle.GetRawText().Should().Be("""{"default":"Contact details","es":"Datos de contacto"}""");
    }
}
