using System.Text.Json;
using Endatix.Modules.Reporting.Features.FormSchema.FormSchema;

namespace Endatix.Modules.Reporting.Tests.Features.FormSchema.FormSchema;

public partial class FormSchemaCompilerTests
{
  [Fact]
  public void CompilePersisted_CalculatedValueNamedLikeQuestion_WritesOwnCodebookEntry()
  {
    // Arrange
    const string definition = """
        {
          "pages": [{ "elements": [{ "type": "ranking", "name": "qRanking", "title": "Rank the fruit", "choices": ["apple", "pear"] }] }],
          "calculatedValues": [{ "name": "qRanking", "expression": "1", "includeIntoResult": true }]
        }
        """;

    // Act
    FormSchemaCompileResult compiled = new FormSchemaCompiler().CompilePersisted(definition);

    // Assert
    ReadCodebookColumn(compiled.CodebookJson, "qRanking").Should().Be(
      """{"parentKey":"qRanking","surveyJsType":"calculatedvalue","exportShape":"calculated","title":{"default":"qRanking"}}""");
  }

  [Fact]
  public void CompilePersisted_MergeModeWithStaleCalculatedEntry_RebuildsEntry()
  {
    // Arrange
    const string definition = """
        {
          "pages": [{ "elements": [{ "type": "text", "name": "qName" }] }],
          "calculatedValues": [{ "name": "totalAmount", "expression": "1", "includeIntoResult": true }]
        }
        """;
    FormSchemaCompiler compiler = new();
    FormSchemaCompileResult initial = compiler.CompilePersisted(definition);
    // The entry an older compiler stored for every calculated column.
    string staleCodebookJson = initial.CodebookJson.Replace(
      ReadCodebookColumn(initial.CodebookJson, "totalAmount"),
      """{"parentKey":"totalAmount","surveyJsType":"unknown","exportShape":"scalar"}""");

    // Act
    FormSchemaCompileResult recompiled = compiler.CompilePersisted(
      definition,
      initial.FlatteningMapJson,
      staleCodebookJson,
      FormSchemaCompileMode.Merge);

    // Assert
    staleCodebookJson.Should().NotBe(initial.CodebookJson);
    ReadCodebookColumn(recompiled.CodebookJson, "totalAmount").Should().Be(
      ReadCodebookColumn(initial.CodebookJson, "totalAmount"));
  }

  private static string ReadCodebookColumn(string codebookJson, string columnKey)
  {
    using JsonDocument codebook = JsonDocument.Parse(codebookJson);
    return codebook.RootElement.GetProperty("columns").GetProperty(columnKey).GetRawText();
  }
}
