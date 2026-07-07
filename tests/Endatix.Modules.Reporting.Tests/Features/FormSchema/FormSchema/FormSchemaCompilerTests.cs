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

  [Fact]
  public void FromJson_RoundTrip_PreservesColumns()
  {
    MergedFormSchema original = new(
    [
      new FormSchemaColumn(
        "ford__red",
        FormSchemaColumnKind.CheckboxChoice,
        "Ford — Red",
        "boolean",
        SourceQuestion: "ford",
        ChoiceValue: "red"),
      new FormSchemaColumn(
        "contacts__0__email",
        FormSchemaColumnKind.PanelDynamicIndex,
        "Email (contacts #1)",
        "string",
        SourceQuestion: "email",
        PanelName: "contacts",
        PanelIndex: 0),
    ]);

    MergedFormSchema restored = MergedFormSchema.FromJson(original.ToJson());

    restored.Columns.Should().BeEquivalentTo(original.Columns);
  }

  [Fact]
  public void MergeAppendOnly_ChainedCalls_DoNotMutateReceiverOrDropKeys()
  {
    FormSchemaColumn firstName = new("firstName", FormSchemaColumnKind.Simple, "First name", "string");
    FormSchemaColumn lastName = new("lastName", FormSchemaColumnKind.Simple, "Last name", "string");
    FormSchemaColumn email = new("email", FormSchemaColumnKind.Simple, "Email", "string");

    MergedFormSchema original = new([firstName]);
    MergedFormSchema withLastName = original.MergeAppendOnly([lastName]);
    MergedFormSchema withEmail = withLastName.MergeAppendOnly([email]);

    withEmail.Columns.Select(column => column.Key).Should().Equal("firstName", "lastName", "email");
    original.Columns.Select(column => column.Key).Should().Equal("firstName");
    withLastName.Columns.Select(column => column.Key).Should().Equal("firstName", "lastName");

    MergedFormSchema mergedAgainFromOriginal = original.MergeAppendOnly([lastName]);
    mergedAgainFromOriginal.Columns.Select(column => column.Key).Should().Equal("firstName", "lastName");
  }
}
