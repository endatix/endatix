using System.Text.Json;
using Endatix.Modules.Reporting.Features.FlattenedSubmission;
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
  [InlineData("slider-definition.json", "slider-expected-keys.json")]
  [InlineData("tagbox-definition.json", "tagbox-expected-keys.json")]
  [InlineData("matrix-definition.json", "matrix-expected-keys.json")]
  [InlineData("matrixdropdown-definition.json", "matrixdropdown-expected-keys.json")]
  [InlineData("matrixdynamic-definition.json", "matrixdynamic-expected-keys.json")]
  [InlineData("calculated-values-definition.json", "calculated-values-expected-keys.json")]
  [InlineData("multilang-title-definition.json", "multilang-title-expected-keys.json")]
  [InlineData("radiogroup-with-checkbox-definition.json", "radiogroup-with-checkbox-expected-keys.json")]
  public void Flatten_ProducesExpectedKeys(string definitionFixture, string expectedKeysFixture)
  {
    var definition = FormSchemaFixtureLoader.LoadDefinition(definitionFixture);
    var limits = definitionFixture.Contains("paneldynamic", StringComparison.Ordinal)
        ? new SchemaCompilationLimits { MaxPanelCount = 2 }
        : definitionFixture.Contains("matrixdynamic", StringComparison.Ordinal)
            ? new SchemaCompilationLimits { MaxMatrixRowCount = 2 }
            : SchemaCompilationLimits.Default;

    var columns =
        FormDefinitionFlattener.Flatten(definition, limits);

    columns.Select(column => column.Key).Should().BeEquivalentTo(
        FormSchemaFixtureLoader.LoadExpectedKeys(expectedKeysFixture),
        options => options.WithStrictOrdering());
  }

  [Fact]
  public void Flatten_SkipsNonDataElements()
  {
    var definition = FormSchemaFixtureLoader.LoadDefinition("simple-definition.json");

    var columns =
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
    using var document = JsonDocument.Parse(json);
    var definition = document.RootElement.Clone();

    // Act
    Action act = () => FormDefinitionFlattener.Flatten(definition);

    // Assert
    var exception = act
        .Should().Throw<SchemaCompilationLimitExceededException>().Which;
    exception.LimitKind.Should().Be(SchemaCompilationLimitKind.DuplicateColumnKey);
    exception.Context.Should().Be("score");
  }

  [Fact]
  public void Flatten_ExceedsMaxNestingDepth_Throws()
  {
    // Arrange
    var definition = FormSchemaFixtureLoader.LoadDefinition("paneldynamic-definition.json");
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
    using var document = JsonDocument.Parse(json);
    var definition = document.RootElement.Clone();
    SchemaCompilationLimits limits = new() { MaxPanelCount = 2 };

    // Act
    var columns = FormDefinitionFlattener.Flatten(definition, limits);

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
    using var document = JsonDocument.Parse(json);
    var definition = document.RootElement.Clone();
    SchemaCompilationLimits limits = new() { MaxChoicesPerQuestion = 2 };

    // Act
    Action act = () => FormDefinitionFlattener.Flatten(definition, limits);

    // Assert
    var exception = act
        .Should().Throw<SchemaCompilationLimitExceededException>().Which;
    exception.LimitKind.Should().Be(SchemaCompilationLimitKind.MaxChoicesPerQuestion);
    exception.Context.Should().Be("satisfaction");
  }

  [Fact]
  public void Flatten_ExceedsMaxLoopCombinations_Throws()
  {
    // Arrange
    var definition = FormSchemaFixtureLoader.LoadDefinition("nested-loop-definition.json");
    SchemaCompilationLimits limits = new() { MaxLoopCombinations = 1 };

    // Act
    Action act = () => FormDefinitionFlattener.Flatten(definition, limits);

    // Assert
    act.Should().Throw<SchemaCompilationLimitExceededException>()
        .Which.LimitKind.Should().Be(SchemaCompilationLimitKind.MaxLoopCombinations);
  }

  [Fact]
  public void Flatten_ExceedsMaxQuestions_Throws()
  {
    const string json = """
        {
          "pages": [
            {
              "elements": [
                { "type": "text", "name": "q1" },
                { "type": "text", "name": "q2" }
              ]
            }
          ],
          "calculatedValues": [
            { "name": "total", "expression": "1" }
          ]
        }
        """;
    using var document = JsonDocument.Parse(json);
    var definition = document.RootElement.Clone();
    SchemaCompilationLimits limits = new() { MaxQuestions = 2 };

    Action act = () => FormDefinitionFlattener.Flatten(definition, limits);

    var exception = act
        .Should().Throw<SchemaCompilationLimitExceededException>().Which;
    exception.LimitKind.Should().Be(SchemaCompilationLimitKind.MaxQuestions);
    exception.Actual.Should().Be(3);
  }

  [Fact]
  public void Flatten_Radiogroup_EmitsSingleNeutralColumn()
  {
    // Arrange
    var definition = FormSchemaFixtureLoader.LoadDefinition("radiogroup-definition.json");

    // Act
    var columns = FormDefinitionFlattener.Flatten(definition);

    // Assert
    columns.Should().ContainSingle();
    var column = columns[0];
    column.Kind.Should().Be(FormSchemaColumnKind.Simple);
    column.Key.Should().Be("carColor");
    column.DataType.Should().Be("string");
    column.MatrixColumnChoices.Should().BeNull();
  }

  [Fact]
  public void Flatten_LoopSourceFile_ColumnKindIsFileUpload()
  {
    const string definitionJson = """
        {
          "pages": [
            {
              "elements": [
                {
                  "type": "checkbox",
                  "name": "brands",
                  "choices": [
                    { "value": "nike", "text": "Nike" }
                  ]
                },
                {
                  "type": "paneldynamic",
                  "name": "brandLoop",
                  "loopSource": ["brands"],
                  "templateElements": [
                    {
                      "type": "file",
                      "name": "brandPhoto",
                      "title": "Brand photo"
                    }
                  ]
                }
              ]
            }
          ]
        }
        """;

    using var definition = JsonDocument.Parse(definitionJson);
    var columns = FormDefinitionFlattener.Flatten(definition.RootElement);

    var column = columns.Single(c => c.Key == "brandLoop__nike__brandPhoto");
    column.Kind.Should().Be(FormSchemaColumnKind.FileUpload);
    column.SourceQuestion.Should().Be("brandPhoto");
    column.LoopPath.Should().NotBeNull();
    column.DataType.Should().Be("file");
  }

  [Fact]
  public void Flatten_LoopSourceMatrixDynamic_GeneratesIndexedMatrixCells()
  {
    const string definitionJson = """
        {
          "pages": [
            {
              "elements": [
                {
                  "type": "checkbox",
                  "name": "brands",
                  "choices": [
                    { "value": "nike", "text": "Nike" }
                  ]
                },
                {
                  "type": "paneldynamic",
                  "name": "brandLoop",
                  "loopSource": ["brands"],
                  "templateElements": [
                    {
                      "type": "matrixdynamic",
                      "name": "employers",
                      "title": "Employers",
                      "rowCount": 2,
                      "columns": [
                        { "name": "company", "title": "Company" },
                        { "name": "years", "title": "Years", "cellType": "text", "inputType": "number" }
                      ]
                    }
                  ]
                }
              ]
            }
          ]
        }
        """;

    using var definition = JsonDocument.Parse(definitionJson);
    var columns = FormDefinitionFlattener.Flatten(definition.RootElement);

    var companyRow0 = columns.Single(c => c.Key == "brandLoop__nike__employers__0__company");
    companyRow0.Kind.Should().Be(FormSchemaColumnKind.MatrixCell);
    companyRow0.PanelIndex.Should().Be(0);
    companyRow0.MatrixColumnValue.Should().Be("company");
    companyRow0.LoopPath.Should().NotBeNull();
    companyRow0.DataType.Should().Be("string");

    var yearsRow1 = columns.Single(c => c.Key == "brandLoop__nike__employers__1__years");
    yearsRow1.Kind.Should().Be(FormSchemaColumnKind.MatrixCell);
    yearsRow1.PanelIndex.Should().Be(1);
    yearsRow1.MatrixColumnValue.Should().Be("years");
    yearsRow1.DataType.Should().Be("number");

    columns.Where(c => c.Key.StartsWith("brandLoop__nike__employers__", StringComparison.Ordinal))
      .Should()
      .HaveCount(4);
  }

  [Fact]
  public void Flatten_FileUpload_ColumnKindIsFileUpload()
  {
    // Arrange
    var definition = FormSchemaFixtureLoader.LoadDefinition("file-definition.json");

    // Act
    var columns = FormDefinitionFlattener.Flatten(definition);

    // Assert
    var column = columns.Should().ContainSingle().Subject;
    column.Kind.Should().Be(FormSchemaColumnKind.FileUpload);
    column.DataType.Should().Be("file");
  }

  [Fact]
  public void Flatten_NumberInput_MapsDataTypeToNumber()
  {
    // Arrange
    var definition = FormSchemaFixtureLoader.LoadDefinition("number-input-definition.json");

    // Act
    var columns = FormDefinitionFlattener.Flatten(definition);

    // Assert
    columns.Single(c => c.Key == "age").DataType.Should().Be("number");
    columns.Single(c => c.Key == "fullName").DataType.Should().Be("string");
  }

  [Fact]
  public void Flatten_Boolean_EmitsSimpleScalarColumn()
  {
    // Arrange
    var definition = FormSchemaFixtureLoader.LoadDefinition("boolean-expression-definition.json");

    // Act
    var columns = FormDefinitionFlattener.Flatten(definition);

    // Assert
    columns.Single(column => column.Key == "isActive").Kind.Should().Be(FormSchemaColumnKind.Simple);
    columns.Single(column => column.Key == "isActive").DataType.Should().Be("boolean");
    columns.Single(column => column.Key == "score").Kind.Should().Be(FormSchemaColumnKind.Simple);
  }

  [Fact]
  public void Flatten_Tagbox_ColumnKindIsChoiceIndicator()
  {
    // Arrange
    var definition = FormSchemaFixtureLoader.LoadDefinition("tagbox-definition.json");

    // Act
    var columns = FormDefinitionFlattener.Flatten(definition);

    // Assert
    columns.Should().AllSatisfy(column =>
    {
      column.Kind.Should().Be(FormSchemaColumnKind.ChoiceIndicator);
    });
  }

  [Fact]
  public void Flatten_TagboxWithoutChoices_EmitsOneColumnForTheSelectedValues()
  {
    const string definitionJson = """
      {"pages":[{"name":"page1","elements":[{"type":"tagbox","name":"questionCities","title":"Cities","choicesLazyLoadEnabled":true}]}]}
      """;
    using var definition = JsonDocument.Parse(definitionJson);

    var columns = FormDefinitionFlattener.Flatten(definition.RootElement);

    var column = columns.Should().ContainSingle().Subject;
    column.Key.Should().Be("questionCities");
    column.Kind.Should().Be(FormSchemaColumnKind.Simple);
    column.SourceQuestion.Should().Be("questionCities");

    MergedFormSchema schema = new(columns);
    using var submission = JsonDocument.Parse("""{"questionCities":["3247449","3408424"]}""");
    var flattened =
      FlattenedSubmissionFlattener.Flatten(submission.RootElement, schema);

    flattened["questionCities"]!.Value.GetRawText().Should().Be("""["3247449","3408424"]""");
  }

  [Fact]
  public void Flatten_TagboxWithoutChoicesAndOtherItem_KeepsValuesColumnAndOtherText()
  {
    const string definitionJson = """
      {"pages":[{"name":"page1","elements":[{"type":"tagbox","name":"questionCities","choicesLazyLoadEnabled":true,"showOtherItem":true}]}]}
      """;
    using var definition = JsonDocument.Parse(definitionJson);

    var columns = FormDefinitionFlattener.Flatten(definition.RootElement);

    columns.Select(column => (column.Key, column.Kind)).Should().Equal(
      ("questionCities", FormSchemaColumnKind.Simple),
      ("questionCities__other_text", FormSchemaColumnKind.CheckboxOtherText));
  }

  [Fact]
  public void Flatten_LoopTagboxWithoutChoicesAndOtherItem_KeepsValuesColumnAndOtherText()
  {
    const string definitionJson = """
      {
        "pages": [{
          "name": "page1",
          "elements": [
            { "type": "checkbox", "name": "brands", "choices": ["nike"] },
            {
              "type": "paneldynamic",
              "name": "brandLoop",
              "loopSource": ["brands"],
              "templateElements": [
                { "type": "tagbox", "name": "cities", "choicesLazyLoadEnabled": true, "showOtherItem": true }
              ]
            }
          ]
        }]
      }
      """;
    using var definition = JsonDocument.Parse(definitionJson);

    var columns = FormDefinitionFlattener.Flatten(definition.RootElement);

    columns.Should().Contain(c => c.Key == "brandLoop__nike__cities" && c.Kind == FormSchemaColumnKind.LoopSource);
    columns.Should().Contain(c =>
      c.Key == "brandLoop__nike__cities__other_text" && c.Kind == FormSchemaColumnKind.CheckboxOtherText);
  }

  [Fact]
  public void Flatten_DeepNestedPanels_CollectsInnerElements()
  {
    // Arrange
    var definition = FormSchemaFixtureLoader.LoadDefinition("nested-panels-definition.json");

    // Act
    var columns = FormDefinitionFlattener.Flatten(definition);

    // Assert
    columns.Select(c => c.Key).Should().BeEquivalentTo(
        ["outerText", "innerText"],
        options => options.WithStrictOrdering());
  }

  [Fact]
  public void Flatten_Matrix_ColumnKindIsMatrixRow()
  {
    // Arrange
    var definition = FormSchemaFixtureLoader.LoadDefinition("matrix-definition.json");

    // Act
    var columns = FormDefinitionFlattener.Flatten(definition);

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
    var definition = FormSchemaFixtureLoader.LoadDefinition("multilang-title-definition.json");

    // Act
    var columns = FormDefinitionFlattener.Flatten(definition);

    // Assert
    var column = columns.Should().ContainSingle().Subject;
    column.Label.Should().Be("fullName");
  }

  [Fact]
  public void Flatten_RadiogroupWithValuePropertyName_IsNotEmitted()
  {
    // Arrange
    var definition = FormSchemaFixtureLoader.LoadDefinition("radiogroup-with-checkbox-definition.json");

    // Act
    var columns = FormDefinitionFlattener.Flatten(definition);

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
    var definition = FormSchemaFixtureLoader.LoadCustomerExcerptDefinition(definitionFixture);

    var columns = FormDefinitionFlattener.Flatten(definition);

    columns.Select(column => column.Key).Should().BeEquivalentTo(
        FormSchemaFixtureLoader.LoadCustomerExcerptExpectedKeys(expectedKeysFixture),
        options => options.WithStrictOrdering());
  }

  [Fact]
  public void Flatten_CheckboxOtherChoiceLabel_IncludesQuestionTitleOnce()
  {
    var definition = FormSchemaFixtureLoader.LoadDefinition("checkbox-definition.json");

    var columns = FormDefinitionFlattener.Flatten(definition);

    columns.Single(column => column.Key == "colors__other").Label.Should().Be("Favorite colors — Other");
  }

  [Fact]
  public void Flatten_LoopSourcePanel_RetainsParentFieldsAndSiblingChildPanels()
  {
    const string definitionJson = """
        {
          "pages": [
            {
              "elements": [
                {
                  "type": "checkbox",
                  "name": "brands",
                  "choices": [
                    { "value": "nike", "text": "Nike" },
                    { "value": "adidas", "text": "Adidas" }
                  ]
                },
                {
                  "type": "paneldynamic",
                  "name": "outerLoop",
                  "loopSource": ["brands"],
                  "templateElements": [
                    {
                      "type": "text",
                      "name": "parentNote",
                      "title": "Parent note"
                    },
                    {
                      "type": "paneldynamic",
                      "name": "innerLoopA",
                      "loopSource": ["brands"],
                      "templateElements": [
                        {
                          "type": "text",
                          "name": "innerAField",
                          "title": "Inner A"
                        }
                      ]
                    },
                    {
                      "type": "paneldynamic",
                      "name": "innerLoopB",
                      "loopSource": ["brands"],
                      "templateElements": [
                        {
                          "type": "text",
                          "name": "innerBField",
                          "title": "Inner B"
                        }
                      ]
                    }
                  ]
                }
              ]
            }
          ]
        }
        """;

    using var definition = JsonDocument.Parse(definitionJson);
    var columns = FormDefinitionFlattener.Flatten(definition.RootElement);
    IReadOnlyCollection<string> keys = columns.Select(column => column.Key).ToArray();

    keys.Should().Contain("outerLoop__nike__parentNote");
    keys.Should().Contain("innerLoopA__nike__nike__innerAField");
    keys.Should().Contain("innerLoopB__adidas__adidas__innerBField");
  }

  [Fact]
  public void Flatten_LoopSourcePanel_UsesValueNameForLoopPathAndNameForOutputKeys()
  {
    const string definitionJson = """
        {
          "pages": [
            {
              "elements": [
                {
                  "type": "checkbox",
                  "name": "brands",
                  "choices": [
                    { "value": "nike", "text": "Nike" }
                  ]
                },
                {
                  "type": "paneldynamic",
                  "name": "brandLoop",
                  "valueName": "brandsLoop",
                  "loopSource": ["brands"],
                  "templateElements": [
                    {
                      "type": "text",
                      "name": "brandNote",
                      "title": "Brand note"
                    }
                  ]
                }
              ]
            }
          ]
        }
        """;

    using var definition = JsonDocument.Parse(definitionJson);
    var columns = FormDefinitionFlattener.Flatten(definition.RootElement);

    var column = columns.Single(c => c.Key == "brandLoop__nike__brandNote");
    column.LoopPath.Should().NotBeNull();
    column.LoopPath![0].PanelValueName.Should().Be("brandsLoop");
  }

  [Fact]
  public void Flatten_MatrixDropdown_ColumnKindIsMatrixCell()
  {
    var definition = FormSchemaFixtureLoader.LoadDefinition("matrixdropdown-definition.json");

    var columns = FormDefinitionFlattener.Flatten(definition);

    columns.Should().AllSatisfy(column => column.Kind.Should().Be(FormSchemaColumnKind.MatrixCell));
    columns.Single(c => c.Key == "orgCount__SPO_small__N_org").DataType.Should().Be("number");
  }

  [Fact]
  public void Flatten_Video_ColumnKindIsFileUpload()
  {
    var definition = FormSchemaFixtureLoader.LoadCustomerExcerptDefinition("f2-unit-panel-definition.json");

    var columns = FormDefinitionFlattener.Flatten(definition);

    var videoColumn = columns.Single(c => c.Key == "verificationVideo");
    videoColumn.Kind.Should().Be(FormSchemaColumnKind.FileUpload);
    videoColumn.DataType.Should().Be("file");
  }

  [Fact]
  public void Flatten_CalculatedValues_ColumnKindIsCalculated()
  {
    var definition = FormSchemaFixtureLoader.LoadDefinition("calculated-values-definition.json");

    var columns = FormDefinitionFlattener.Flatten(definition);

    columns.Single(c => c.Key == "totalAmount").Kind.Should().Be(FormSchemaColumnKind.Calculated);
  }
}
