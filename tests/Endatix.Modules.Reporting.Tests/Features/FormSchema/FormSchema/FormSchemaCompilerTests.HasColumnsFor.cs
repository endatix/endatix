using Endatix.Modules.Reporting.Features.FormSchema.FormSchema;

namespace Endatix.Modules.Reporting.Tests.Features.FormSchema.FormSchema;

public partial class FormSchemaCompilerTests
{
  private const string OlderDefinition = """
      { "pages": [ { "elements": [ { "type": "text", "name": "q1" } ] } ] }
      """;

  private const string NewerDefinition = """
      { "pages": [ { "elements": [ { "type": "text", "name": "q2" } ] } ] }
      """;

  [Fact]
  public void HasColumnsFor_WithEveryColumnMergedIn_ReturnsTrue()
  {
    // Arrange
    FormSchemaCompiler compiler = new();
    var older = compiler.CompilePersisted(OlderDefinition);
    var merged = compiler.CompilePersisted(NewerDefinition, older.FlatteningMapJson, older.CodebookJson);

    // Act
    var hasColumns = compiler.HasColumnsFor(merged.FlatteningMapJson, OlderDefinition);

    // Assert
    hasColumns.Should().BeTrue();
  }

  [Fact]
  public void HasColumnsFor_WithColumnDroppedByReplace_ReturnsFalse()
  {
    // Arrange
    FormSchemaCompiler compiler = new();
    var replaced = compiler.CompilePersisted(NewerDefinition, mode: FormSchemaCompileMode.Replace);

    // Act
    var hasColumns = compiler.HasColumnsFor(replaced.FlatteningMapJson, OlderDefinition);

    // Assert
    hasColumns.Should().BeFalse();
  }
}
