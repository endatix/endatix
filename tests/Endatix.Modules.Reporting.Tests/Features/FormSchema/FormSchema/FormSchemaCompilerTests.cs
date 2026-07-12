using System.Text.Json;
using Endatix.Modules.Reporting.Features.FormSchema.FormSchema;

namespace Endatix.Modules.Reporting.Tests.Features.FormSchema.FormSchema;

public class FormSchemaCompilerTests
{
  [Fact]
  public void CompilePersisted_MergeChoiceCatalog_PreservesHistoricalIdsWithCurrentMetadata()
  {
    const string initialDefinition = """
        {
          "pages": [
            {
              "elements": [
                {
                  "type": "dropdown",
                  "name": "qDropdown",
                  "title": "Weapon",
                  "choices": [
                    { "value": "axe", "text": "Axe" },
                    { "value": "sword", "text": "Sword" },
                    { "value": "hammer", "text": "Hammer" }
                  ]
                }
              ]
            }
          ]
        }
        """;

    const string updatedDefinition = """
        {
          "pages": [
            {
              "elements": [
                {
                  "type": "dropdown",
                  "name": "qDropdown",
                  "title": "Weapon",
                  "choices": [
                    { "value": "axe", "text": "Battle Axe" },
                    { "value": "sword", "text": "Sword" },
                    { "value": "hammer", "text": "Hammer" },
                    { "value": "spear", "text": "Spear" }
                  ]
                }
              ]
            }
          ]
        }
        """;

    FormSchemaCompiler compiler = new();
    FormSchemaCompileResult initial = compiler.CompilePersisted(initialDefinition);
    FormSchemaCompileResult merged = compiler.CompilePersisted(
      updatedDefinition,
      initial.FlatteningMapJson,
      initial.CodebookJson);

    using JsonDocument codebook = JsonDocument.Parse(merged.CodebookJson);
    JsonElement choices = codebook.RootElement
      .GetProperty("choiceCatalogs")
      .GetProperty("qDropdown")
      .GetProperty("choices");

    List<JsonElement> choiceEntries = choices.EnumerateArray().ToList();
    choiceEntries.Should().HaveCount(4);
    choiceEntries.Select(choice => choice.GetProperty("id").GetInt32()).Should().Equal(1, 2, 3, 4);
    choiceEntries[0].GetProperty("value").GetString().Should().Be("axe");
    choiceEntries[0].GetProperty("text").GetProperty("default").GetString().Should().Be("Battle Axe");
    choiceEntries[3].GetProperty("value").GetString().Should().Be("spear");
    choiceEntries[3].GetProperty("id").GetInt32().Should().Be(4);
  }

  [Fact]
  public void CompilePersisted_MergeChoiceCatalog_RetainsHistoricalOnlyChoices()
  {
    const string initialDefinition = """
        {
          "pages": [
            {
              "elements": [
                {
                  "type": "dropdown",
                  "name": "qDropdown",
                  "choices": [
                    { "value": "axe", "text": "Axe" },
                    { "value": "sword", "text": "Sword" },
                    { "value": "hammer", "text": "Hammer" }
                  ]
                }
              ]
            }
          ]
        }
        """;

    const string updatedDefinition = """
        {
          "pages": [
            {
              "elements": [
                {
                  "type": "dropdown",
                  "name": "qDropdown",
                  "choices": [
                    { "value": "axe", "text": "Battle Axe" },
                    { "value": "sword", "text": "Sword" }
                  ]
                }
              ]
            }
          ]
        }
        """;

    FormSchemaCompiler compiler = new();
    FormSchemaCompileResult initial = compiler.CompilePersisted(initialDefinition);
    FormSchemaCompileResult merged = compiler.CompilePersisted(
      updatedDefinition,
      initial.FlatteningMapJson,
      initial.CodebookJson);

    using JsonDocument codebook = JsonDocument.Parse(merged.CodebookJson);
    JsonElement choices = codebook.RootElement
      .GetProperty("choiceCatalogs")
      .GetProperty("qDropdown")
      .GetProperty("choices");

    List<JsonElement> choiceEntries = choices.EnumerateArray().ToList();
    choiceEntries.Should().HaveCount(3);
    choiceEntries.Select(choice => choice.GetProperty("id").GetInt32()).Should().Equal(1, 2, 3);
    choiceEntries.Single(choice => choice.GetProperty("value").GetString() == "hammer")
      .GetProperty("text").GetProperty("default").GetString().Should().Be("Hammer");
    choiceEntries.Single(choice => choice.GetProperty("value").GetString() == "axe")
      .GetProperty("text").GetProperty("default").GetString().Should().Be("Battle Axe");
  }

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
        FormSchemaColumnKind.ChoiceIndicator,
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

  [Fact]
  public void FromJson_SkipsInvalidColumnsAndPreservesValidOnes()
  {
    const string json = """
        [
          { "key": "valid", "kind": "Simple", "label": "Valid", "dataType": "string" },
          { "kind": "Simple", "label": "Missing key" },
          { "key": "legacy", "kind": "RetiredKind", "label": "Legacy", "dataType": "string" },
          {
            "key": "brokenLoop",
            "kind": "NestedLoop",
            "label": "Broken loop",
            "dataType": "string",
            "loopPath": [{ "panelValueName": "cars" }]
          }
        ]
        """;

    MergedFormSchema schema = MergedFormSchema.FromJson(json);

    schema.Columns.Select(column => column.Key).Should().Equal("valid");
  }
}
