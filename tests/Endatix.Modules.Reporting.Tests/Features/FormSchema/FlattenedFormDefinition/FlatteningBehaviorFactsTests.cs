using System.Text.Json;
using Endatix.Modules.Reporting.Features.FlattenedSubmission;
using Endatix.Modules.Reporting.Features.FormSchema.FlattenedFormDefinition;
using Endatix.Modules.Reporting.Features.FormSchema.FormSchema;
using Endatix.Modules.Reporting.Tests.Features.FormSchema.FormSchema;

namespace Endatix.Modules.Reporting.Tests.Features.FormSchema.FlattenedFormDefinition;

/// <summary>
/// Catalog of flattening behavior facts mapped to golden fixtures.
/// Each fact ID corresponds to the BI export plan specification.
/// </summary>
public class FlatteningBehaviorFactsTests
{
    public static TheoryData<string, string, string> DefinitionFacts =>
        new()
        {
            { "D-01", "simple-definition.json", "simple-expected-keys.json" },
            { "D-01", "radiogroup-definition.json", "radiogroup-expected-keys.json" },
            { "D-01", "number-input-definition.json", "number-input-expected-keys.json" },
            { "D-02", "checkbox-definition.json", "checkbox-expected-keys.json" },
            { "D-02", "tagbox-definition.json", "tagbox-expected-keys.json" },
            { "D-03", "ranking-definition.json", "ranking-expected-keys.json" },
            { "D-04", "matrix-definition.json", "matrix-expected-keys.json" },
            { "D-05", "matrixdropdown-definition.json", "matrixdropdown-expected-keys.json" },
            { "D-06", "matrixdynamic-definition.json", "matrixdynamic-expected-keys.json" },
            { "D-07", "multipletext-definition.json", "multipletext-expected-keys.json" },
            { "D-08", "file-definition.json", "file-expected-keys.json" },
            { "D-09", "boolean-expression-definition.json", "boolean-expression-expected-keys.json" },
            { "D-09b", "slider-definition.json", "slider-expected-keys.json" },
            { "D-10", "calculated-values-definition.json", "calculated-values-expected-keys.json" },
            { "D-11", "nested-panels-definition.json", "nested-panels-expected-keys.json" },
            { "D-12", "CustomerExcerpts/f1-radiogroup-page-definition.json", "CustomerExcerpts/f1-radiogroup-page-expected-keys.json" },
            { "D-13", "paneldynamic-definition.json", "paneldynamic-expected-keys.json" },
            { "D-14", "nested-loop-definition.json", "nested-loop-expected-keys.json" },
            { "D-15", "radiogroup-with-checkbox-definition.json", "radiogroup-with-checkbox-expected-keys.json" },
            { "D-17", "CustomerExcerpts/f2-unit-panel-definition.json", "CustomerExcerpts/f2-unit-panel-expected-keys.json" },
        };

    public static TheoryData<string, string, string, string> SubmissionFacts =>
        new()
        {
            { "S-01", "simple-definition.json", "simple-submission.json", "simple-expected-flat.json" },
            { "S-02", "checkbox-definition.json", "checkbox-submission.json", "checkbox-expected-flat.json" },
            { "S-04", "ranking-definition.json", "ranking-submission.json", "ranking-expected-flat.json" },
            { "S-06", "matrixdropdown-definition.json", "matrixdropdown-submission.json", "matrixdropdown-expected-flat.json" },
            { "S-07", "matrixdynamic-definition.json", "matrixdynamic-submission.json", "matrixdynamic-expected-flat.json" },
            { "S-08", "multipletext-definition.json", "multipletext-submission.json", "multipletext-expected-flat.json" },
            { "S-09", "slider-definition.json", "slider-submission.json", "slider-expected-flat.json" },
            { "S-10", "paneldynamic-definition.json", "paneldynamic-submission.json", "paneldynamic-expected-flat.json" },
            { "S-11", "nested-loop-definition.json", "nested-loop-submission.json", "nested-loop-expected-flat.json" },
        };

    [Theory]
    [MemberData(nameof(DefinitionFacts))]
    public void FormDefinitionFlattener_Flatten_WithBehaviorDefinitionFixture_ProducesExpectedColumnKeys(string factId, string definitionFixture, string expectedKeysFixture)
    {
        JsonElement definition = LoadDefinitionFixture(definitionFixture);
        SchemaCompilationLimits limits = ResolveLimits(definitionFixture);

        IReadOnlyList<FormSchemaColumn> columns = FormDefinitionFlattener.Flatten(definition, limits);
        IReadOnlyList<string> expectedKeys = LoadExpectedKeysFixture(expectedKeysFixture);

        columns.Select(column => column.Key).Should().BeEquivalentTo(
            expectedKeys,
            options => options.WithStrictOrdering(),
            because: $"behavior fact {factId} should produce the documented column keys");
    }

    [Theory]
    [MemberData(nameof(SubmissionFacts))]
    public void FlattenedSubmissionFlattener_Flatten_WithBehaviorSubmissionFixture_MatchesExpectedFlatOutput(
        string factId,
        string definitionFixture,
        string submissionFixture,
        string expectedFlatFixture)
    {
        JsonElement definition = LoadDefinitionFixture(definitionFixture);
        SchemaCompilationLimits limits = ResolveLimits(definitionFixture);
        MergedFormSchema formSchema = new(FormDefinitionFlattener.Flatten(definition, limits));
        JsonElement submission = FormSchemaFixtureLoader.LoadJson(submissionFixture);
        JsonElement expected = FormSchemaFixtureLoader.LoadJson(expectedFlatFixture);

        Dictionary<string, JsonElement?> flattened =
            FlattenedSubmissionFlattener.Flatten(submission, formSchema);

        FormSchemaFixtureAssertions.AssertFlatMatchesExpected(
            flattened,
            expected,
            because: $"behavior fact {factId}");
    }

    private static JsonElement LoadDefinitionFixture(string fixturePath) =>
        fixturePath.StartsWith("CustomerExcerpts/", StringComparison.Ordinal)
            ? FormSchemaFixtureLoader.LoadCustomerExcerptDefinition(fixturePath["CustomerExcerpts/".Length..])
            : FormSchemaFixtureLoader.LoadDefinition(fixturePath);

    private static IReadOnlyList<string> LoadExpectedKeysFixture(string fixturePath) =>
        fixturePath.StartsWith("CustomerExcerpts/", StringComparison.Ordinal)
            ? FormSchemaFixtureLoader.LoadCustomerExcerptExpectedKeys(fixturePath["CustomerExcerpts/".Length..])
            : FormSchemaFixtureLoader.LoadExpectedKeys(fixturePath);

    private static SchemaCompilationLimits ResolveLimits(string definitionFixture)
    {
        if (definitionFixture.Contains("paneldynamic", StringComparison.Ordinal))
        {
            return new SchemaCompilationLimits { MaxPanelCount = 2 };
        }

        if (definitionFixture.Contains("matrixdynamic", StringComparison.Ordinal))
        {
            return new SchemaCompilationLimits { MaxMatrixRowCount = 2 };
        }

        return SchemaCompilationLimits.Default;
    }
}
