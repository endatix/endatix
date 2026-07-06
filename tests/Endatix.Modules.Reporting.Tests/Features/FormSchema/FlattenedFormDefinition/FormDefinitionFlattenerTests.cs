using System.Text.Json;
using Endatix.Modules.Reporting.Features.FormSchema.FlattenedFormDefinition;
using Endatix.Modules.Reporting.Features.FormSchema.FormSchema;
using Endatix.Modules.Reporting.Tests.Features.FormSchema.FormSchema;

namespace Endatix.Modules.Reporting.Tests.Features.FormSchema.FlattenedFormDefinition;

public class FormDefinitionFlattenerTests
{
  [Theory]
  [InlineData("simple-definition.json", "simple-expected-keys.json")]
  [InlineData("checkbox-definition.json", "checkbox-expected-keys.json")]
  [InlineData("paneldynamic-definition.json", "paneldynamic-expected-keys.json")]
  [InlineData("nested-loop-definition.json", "nested-loop-expected-keys.json")]
  [InlineData("ranking-definition.json", "ranking-expected-keys.json")]
  [InlineData("multipletext-definition.json", "multipletext-expected-keys.json")]
  [InlineData("radiogroup-definition.json", "radiogroup-expected-keys.json")]
  [InlineData("file-definition.json", "file-expected-keys.json")]
  [InlineData("number-input-definition.json", "number-input-expected-keys.json")]
  [InlineData("nested-panels-definition.json", "nested-panels-expected-keys.json")]
  [InlineData("boolean-expression-definition.json", "boolean-expression-expected-keys.json")]
  [InlineData("tagbox-definition.json", "tagbox-expected-keys.json")]
  [InlineData("matrix-definition.json", "matrix-expected-keys.json")]
  [InlineData("matrixdropdown-definition.json", "matrixdropdown-expected-keys.json")]
  [InlineData("matrixdynamic-definition.json", "matrixdynamic-expected-keys.json")]
  [InlineData("calculated-values-definition.json", "calculated-values-expected-keys.json")]
  [InlineData("multilang-title-definition.json", "multilang-title-expected-keys.json")]
  [InlineData("radiogroup-with-checkbox-definition.json", "radiogroup-with-checkbox-expected-keys.json")]
  public void Flatten_ProducesExpectedKeys(string definitionFixture, string expectedKeysFixture)
  {
    JsonElement definition = FormSchemaFixtureLoader.LoadDefinition(definitionFixture);
    SchemaCompilationLimits limits = definitionFixture.Contains("paneldynamic", StringComparison.Ordinal)
        ? new SchemaCompilationLimits { MaxPanelCount = 2 }
        : definitionFixture.Contains("matrixdynamic", StringComparison.Ordinal)
            ? new SchemaCompilationLimits { MaxMatrixRowCount = 2 }
            : SchemaCompilationLimits.Default;

    IReadOnlyList<FormSchemaColumn> columns =
        FormDefinitionFlattener.Flatten(definition, limits);

    columns.Select(column => column.Key).Should().BeEquivalentTo(
        FormSchemaFixtureLoader.LoadExpectedKeys(expectedKeysFixture),
        options => options.WithStrictOrdering());
  }

  [Fact]
  public void Flatten_SkipsNonDataElements()
  {
    JsonElement definition = FormSchemaFixtureLoader.LoadDefinition("simple-definition.json");

    IReadOnlyList<FormSchemaColumn> columns =
        FormDefinitionFlattener.Flatten(definition);

    columns.Should().NotContain(column => column.Key == "info");
  }

  [Fact]
  public void Flatten_DuplicateColumnKey_Throws()
  {
    // Arrange
    const string json = """
            {
              "pages": [
                {
                  "elements": [
                    { "type": "text", "name": "score" }
                  ]
                }
              ],
              "calculatedValues": [
                { "name": "score", "expression": "1" }
              ]
            }
            """;
    using JsonDocument document = JsonDocument.Parse(json);
    JsonElement definition = document.RootElement.Clone();

    // Act
    Action act = () => FormDefinitionFlattener.Flatten(definition);

    // Assert
    SchemaCompilationLimitExceededException exception = act
        .Should().Throw<SchemaCompilationLimitExceededException>().Which;
    exception.LimitKind.Should().Be(SchemaCompilationLimitKind.DuplicateColumnKey);
    exception.Context.Should().Be("score");
  }

  [Fact]
  public void Flatten_ExceedsMaxNestingDepth_Throws()
  {
    // Arrange
    JsonElement definition = FormSchemaFixtureLoader.LoadDefinition("paneldynamic-definition.json");
    SchemaCompilationLimits limits = new() { MaxNestingDepth = 0, MaxPanelCount = 2 };

    // Act
    Action act = () => FormDefinitionFlattener.Flatten(definition, limits);

    // Assert
    act.Should().Throw<SchemaCompilationLimitExceededException>()
        .Which.LimitKind.Should().Be(SchemaCompilationLimitKind.MaxNestingDepth);
  }

  [Fact]
  public void Flatten_CapsSurveyMaxPanelCountByLimits()
  {
    // Arrange
    const string json = """
            {
              "pages": [
                {
                  "elements": [
                    {
                      "type": "paneldynamic",
                      "name": "contacts",
                      "maxPanelCount": 50,
                      "templateElements": [
                        { "type": "text", "name": "email" }
                      ]
                    }
                  ]
                }
              ]
            }
            """;
    using JsonDocument document = JsonDocument.Parse(json);
    JsonElement definition = document.RootElement.Clone();
    SchemaCompilationLimits limits = new() { MaxPanelCount = 2 };

    // Act
    IReadOnlyList<FormSchemaColumn> columns = FormDefinitionFlattener.Flatten(definition, limits);

    // Assert
    columns.Select(column => column.Key).Should().BeEquivalentTo(
    [
        "contacts__0__email",
            "contacts__1__email",
        ]);
  }

  [Fact]
  public void Flatten_ExceedsMaxChoicesPerQuestion_Matrix_Throws()
  {
    // Arrange
    const string json = """
            {
              "pages": [
                {
                  "elements": [
                    {
                      "type": "matrix",
                      "name": "satisfaction",
                      "rows": ["r1", "r2", "r3"]
                    }
                  ]
                }
              ]
            }
            """;
    using JsonDocument document = JsonDocument.Parse(json);
    JsonElement definition = document.RootElement.Clone();
    SchemaCompilationLimits limits = new() { MaxChoicesPerQuestion = 2 };

    // Act
    Action act = () => FormDefinitionFlattener.Flatten(definition, limits);

    // Assert
    SchemaCompilationLimitExceededException exception = act
        .Should().Throw<SchemaCompilationLimitExceededException>().Which;
    exception.LimitKind.Should().Be(SchemaCompilationLimitKind.MaxChoicesPerQuestion);
    exception.Context.Should().Be("satisfaction");
  }

  [Fact]
  public void Flatten_ExceedsMaxLoopCombinations_Throws()
  {
    // Arrange
    JsonElement definition = FormSchemaFixtureLoader.LoadDefinition("nested-loop-definition.json");
    SchemaCompilationLimits limits = new() { MaxLoopCombinations = 1 };

    // Act
    Action act = () => FormDefinitionFlattener.Flatten(definition, limits);

    // Assert
    act.Should().Throw<SchemaCompilationLimitExceededException>()
        .Which.LimitKind.Should().Be(SchemaCompilationLimitKind.MaxLoopCombinations);
  }

  [Fact]
  public void Flatten_Radiogroup_ColumnKindIsSimple()
  {
    // Arrange
    JsonElement definition = FormSchemaFixtureLoader.LoadDefinition("radiogroup-definition.json");

    // Act
    IReadOnlyList<FormSchemaColumn> columns = FormDefinitionFlattener.Flatten(definition);

    // Assert
    FormSchemaColumn column = columns.Should().ContainSingle().Subject;
    column.Kind.Should().Be(FormSchemaColumnKind.Simple);
  }

  [Fact]
  public void Flatten_FileUpload_ColumnKindIsFileUpload()
  {
    // Arrange
    JsonElement definition = FormSchemaFixtureLoader.LoadDefinition("file-definition.json");

    // Act
    IReadOnlyList<FormSchemaColumn> columns = FormDefinitionFlattener.Flatten(definition);

    // Assert
    FormSchemaColumn column = columns.Should().ContainSingle().Subject;
    column.Kind.Should().Be(FormSchemaColumnKind.FileUpload);
    column.DataType.Should().Be("file");
  }

  [Fact]
  public void Flatten_NumberInput_MapsDataTypeToNumber()
  {
    // Arrange
    JsonElement definition = FormSchemaFixtureLoader.LoadDefinition("number-input-definition.json");

    // Act
    IReadOnlyList<FormSchemaColumn> columns = FormDefinitionFlattener.Flatten(definition);

    // Assert
    columns.Single(c => c.Key == "age").DataType.Should().Be("number");
    columns.Single(c => c.Key == "fullName").DataType.Should().Be("string");
  }

  [Fact]
  public void Flatten_Boolean_HasBooleanDataType()
  {
    // Arrange
    JsonElement definition = FormSchemaFixtureLoader.LoadDefinition("boolean-expression-definition.json");

    // Act
    IReadOnlyList<FormSchemaColumn> columns = FormDefinitionFlattener.Flatten(definition);

    // Assert
    columns.Single(c => c.Key == "isActive").DataType.Should().Be("boolean");
    columns.Single(c => c.Key == "score").Kind.Should().Be(FormSchemaColumnKind.Simple);
  }

  [Fact]
  public void Flatten_Tagbox_ColumnKindIsCheckboxChoice()
  {
    // Arrange
    JsonElement definition = FormSchemaFixtureLoader.LoadDefinition("tagbox-definition.json");

    // Act
    IReadOnlyList<FormSchemaColumn> columns = FormDefinitionFlattener.Flatten(definition);

    // Assert
    columns.Should().AllSatisfy(column =>
    {
      column.Kind.Should().Be(FormSchemaColumnKind.CheckboxChoice);
    });
  }

  [Fact]
  public void Flatten_DeepNestedPanels_CollectsInnerElements()
  {
    // Arrange
    JsonElement definition = FormSchemaFixtureLoader.LoadDefinition("nested-panels-definition.json");

    // Act
    IReadOnlyList<FormSchemaColumn> columns = FormDefinitionFlattener.Flatten(definition);

    // Assert
    columns.Select(c => c.Key).Should().BeEquivalentTo(
        ["outerText", "innerText"],
        options => options.WithStrictOrdering());
  }

  [Fact]
  public void Flatten_Matrix_ColumnKindIsMatrixRow()
  {
    // Arrange
    JsonElement definition = FormSchemaFixtureLoader.LoadDefinition("matrix-definition.json");

    // Act
    IReadOnlyList<FormSchemaColumn> columns = FormDefinitionFlattener.Flatten(definition);

    // Assert
    columns.Should().AllSatisfy(column =>
    {
      column.Kind.Should().Be(FormSchemaColumnKind.MatrixRow);
    });
  }

  [Fact]
  public void Flatten_MultiLanguageTitle_FallsBackToName()
  {
    // Arrange
    JsonElement definition = FormSchemaFixtureLoader.LoadDefinition("multilang-title-definition.json");

    // Act
    IReadOnlyList<FormSchemaColumn> columns = FormDefinitionFlattener.Flatten(definition);

    // Assert
    FormSchemaColumn column = columns.Should().ContainSingle().Subject;
    column.Label.Should().Be("fullName");
  }

  [Fact]
  public void Flatten_RadiogroupWithValuePropertyName_IsNotEmitted()
  {
    // Arrange
    JsonElement definition = FormSchemaFixtureLoader.LoadDefinition("radiogroup-with-checkbox-definition.json");

    // Act
    IReadOnlyList<FormSchemaColumn> columns = FormDefinitionFlattener.Flatten(definition);

    // Assert
    columns.Should().NotContain(c => c.Key == "drivingRg");
  }

  [Theory]
  [InlineData("f1-radiogroup-page-definition.json", "f1-radiogroup-page-expected-keys.json")]
  [InlineData("f1-barcode-page-definition.json", "f1-barcode-page-expected-keys.json")]
  [InlineData("f2-unit-panel-definition.json", "f2-unit-panel-expected-keys.json")]
  [InlineData("f3-rating-matrix-pair-definition.json", "f3-rating-matrix-pair-expected-keys.json")]
  [InlineData("f3-ranking-definition.json", "f3-ranking-expected-keys.json")]
  [InlineData("f3-multipletext-definition.json", "f3-multipletext-expected-keys.json")]
  public void Flatten_CustomerExcerpt_ProducesExpectedKeys(string definitionFixture, string expectedKeysFixture)
  {
    JsonElement definition = FormSchemaFixtureLoader.LoadCustomerExcerptDefinition(definitionFixture);

    IReadOnlyList<FormSchemaColumn> columns = FormDefinitionFlattener.Flatten(definition);

    columns.Select(column => column.Key).Should().BeEquivalentTo(
        FormSchemaFixtureLoader.LoadCustomerExcerptExpectedKeys(expectedKeysFixture),
        options => options.WithStrictOrdering());
  }

  [Fact]
  public void Flatten_MatrixDropdown_ColumnKindIsMatrixCell()
  {
    JsonElement definition = FormSchemaFixtureLoader.LoadDefinition("matrixdropdown-definition.json");

    IReadOnlyList<FormSchemaColumn> columns = FormDefinitionFlattener.Flatten(definition);

    columns.Should().AllSatisfy(column => column.Kind.Should().Be(FormSchemaColumnKind.MatrixCell));
    columns.Single(c => c.Key == "orgCount__SPO_small__N_org").DataType.Should().Be("number");
  }

  [Fact]
  public void Flatten_Video_ColumnKindIsFileUpload()
  {
    JsonElement definition = FormSchemaFixtureLoader.LoadCustomerExcerptDefinition("f2-unit-panel-definition.json");

    IReadOnlyList<FormSchemaColumn> columns = FormDefinitionFlattener.Flatten(definition);

    FormSchemaColumn videoColumn = columns.Single(c => c.Key == "verificationVideo");
    videoColumn.Kind.Should().Be(FormSchemaColumnKind.FileUpload);
    videoColumn.DataType.Should().Be("file");
  }

  [Fact]
  public void Flatten_CalculatedValues_ColumnKindIsCalculated()
  {
    JsonElement definition = FormSchemaFixtureLoader.LoadDefinition("calculated-values-definition.json");

    IReadOnlyList<FormSchemaColumn> columns = FormDefinitionFlattener.Flatten(definition);

    columns.Single(c => c.Key == "totalAmount").Kind.Should().Be(FormSchemaColumnKind.Calculated);
  }
}
