using System.Text.Json;
using Endatix.Modules.Reporting.Features.FormSchema.FormSchema;

namespace Endatix.Modules.Reporting.Tests.Features.FormSchema.FormSchema;

public class FormSchemaCompilerTests
{
  [Fact]
  public void Compile_AppendOnlyMerge_PreservesHistoricalKeys()
  {
    const string versionOne = """
            {
              "pages": [
                {
                  "elements": [
                    { "type": "text", "name": "firstName", "title": "First name" }
                  ]
                }
              ]
            }
            """;

    const string versionTwo = """
            {
              "pages": [
                {
                  "elements": [
                    { "type": "text", "name": "firstName", "title": "First name" },
                    { "type": "text", "name": "lastName", "title": "Last name" }
                  ]
                }
              ]
            }
            """;

    FormSchemaCompiler compiler = new();
    MergedFormSchema firstPass = compiler.Compile(versionOne);
    MergedFormSchema merged = compiler.Compile(versionTwo, firstPass);

    merged.Columns.Select(column => column.Key).Should().Equal("firstName", "lastName");
  }

  [Fact]
  public void Constructor_DeduplicatesColumnsByKey_KeepsFirstOccurrence()
  {
    MergedFormSchema schema = new(
    [
      new FormSchemaColumn("score", FormSchemaColumnKind.Simple, "First", "string"),
      new FormSchemaColumn("score", FormSchemaColumnKind.Calculated, "Duplicate", "string"),
      new FormSchemaColumn("name", FormSchemaColumnKind.Simple, "Name", "string"),
    ]);

    schema.Columns.Select(column => column.Key).Should().Equal("score", "name");
    schema.Columns.Single(column => column.Key == "score").Label.Should().Be("First");
  }
}
