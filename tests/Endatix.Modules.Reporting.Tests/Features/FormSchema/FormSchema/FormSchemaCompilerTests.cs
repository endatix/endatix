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
}
