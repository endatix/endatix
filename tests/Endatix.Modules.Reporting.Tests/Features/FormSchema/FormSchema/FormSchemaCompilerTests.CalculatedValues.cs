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

  [Fact]
  public void CompilePersisted_MergeModeAfterQuestionTookOverCalculatedValueName_WritesQuestionEntry()
  {
    // Arrange
    const string v1 = """
        {
          "pages": [{ "elements": [{ "type": "text", "name": "qName" }] }],
          "calculatedValues": [{ "name": "score", "expression": "1", "includeIntoResult": true }]
        }
        """;
    const string v2 = """
        { "pages": [{ "elements": [{ "type": "text", "name": "qName" }, { "type": "text", "name": "score", "title": "Your score" }] }] }
        """;
    FormSchemaCompiler compiler = new();
    FormSchemaCompileResult initial = compiler.CompilePersisted(v1);

    // Act
    FormSchemaCompileResult merged = compiler.CompilePersisted(
      v2,
      initial.FlatteningMapJson,
      initial.CodebookJson,
      FormSchemaCompileMode.Merge);

    // Assert: the stored map keeps the first kind; the codebook describes the current question.
    merged.FlatteningMap.Columns.Single(column => column.Key == "score").Kind.Should().Be(FormSchemaColumnKind.Calculated);
    ReadCodebookColumn(merged.CodebookJson, "score").Should().Be(
      """{"parentKey":"score","surveyJsType":"text","exportShape":"scalar","title":{"default":"Your score"}}""");
  }

  [Theory]
  [InlineData(FormSchemaCompileMode.Merge, true)]
  [InlineData(FormSchemaCompileMode.Replace, false)]
  public void CompilePersisted_StoredColumnForUnsavedCalculatedValue_KeptOnlyByMerge(FormSchemaCompileMode mode, bool kept)
  {
    // Arrange: older compilers treated a missing includeIntoResult as true and stored a column.
    const string storedDefinition = """
        {
          "pages": [{ "elements": [{ "type": "text", "name": "qName" }] }],
          "calculatedValues": [{ "name": "totalAmount", "expression": "1", "includeIntoResult": true }]
        }
        """;
    const string definition = """
        {
          "pages": [{ "elements": [{ "type": "text", "name": "qName" }] }],
          "calculatedValues": [{ "name": "totalAmount", "expression": "1" }]
        }
        """;
    FormSchemaCompiler compiler = new();
    FormSchemaCompileResult stored = compiler.CompilePersisted(storedDefinition);

    // Act
    FormSchemaCompileResult recompiled = compiler.CompilePersisted(definition, stored.FlatteningMapJson, stored.CodebookJson, mode);

    // Assert
    recompiled.FlatteningMap.Columns.Any(column => column.Key == "totalAmount").Should().Be(kept);
  }

  private static string ReadCodebookColumn(string codebookJson, string columnKey)
  {
    using JsonDocument codebook = JsonDocument.Parse(codebookJson);
    return codebook.RootElement.GetProperty("columns").GetProperty(columnKey).GetRawText();
  }
}
