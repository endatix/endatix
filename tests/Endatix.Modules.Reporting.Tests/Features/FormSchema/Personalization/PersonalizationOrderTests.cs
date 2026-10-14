using System.Text.Json;
using Endatix.Modules.Reporting.Contracts.Export;
using Endatix.Modules.Reporting.Features.FormSchema.FormSchema;
using Endatix.Modules.Reporting.Tests.Features.FormSchema.FormSchema;
using FluentAssertions;

namespace Endatix.Modules.Reporting.Tests.Features.FormSchema.Personalization;

/// <summary>
/// Flattens run as independent jobs in any order, interleaved with definition rebuilds. Whatever the order, a form
/// ends with the same audience columns, the same types and the same values; only labels may differ until they follow
/// the audience properties.
/// </summary>
[Trait("Category", "Golden")]
public sealed class PersonalizationOrderTests
{
    private static readonly string _definition = FormSchemaFixtureLoader.LoadPersonalizationText("personalization-definition.json");
    private static readonly string _definitionV2 = FormSchemaFixtureLoader.LoadPersonalizationText("personalization-definition-v2.json");
    private static readonly IReadOnlyList<FixtureResponse> _responses = PersonalizationPipeline.LoadResponses();

    public static TheoryData<int> Orders()
    {
        TheoryData<int> orders = [];
        foreach (var index in Enumerable.Range(0, Permutations(_responses).Count()))
        {
            orders.Add(index);
        }

        return orders;
    }

    [Theory]
    [MemberData(nameof(Orders))]
    public void FlattenAll_InAnyOrder_EndsWithTheSameColumnsTypesAndValues(int order)
    {
        // Arrange
        var expected = PersonalizationGoldenTests.FlattenedInFileOrder();
        var responses = Permutations(_responses).ElementAt(order);
        var pipeline = PersonalizationPipeline.Compile(_definition);

        // Act
        pipeline.FlattenAll(_definition, responses);

        // Assert
        pipeline.ExportKeys(ColumnAliasProfile.Native).Should().Equal(expected.ExportKeys(ColumnAliasProfile.Native));
        pipeline.ExportKeys(ColumnAliasProfile.Crunch).Should().Equal(expected.ExportKeys(ColumnAliasProfile.Crunch));
        SortedTypes(pipeline).Should().Equal(SortedTypes(expected));
        foreach (var response in _responses)
        {
            Values(pipeline.Rows[response.Id]).Should().Equal(Values(expected.Rows[response.Id]), response.Id);
        }
    }

    [Fact]
    public void Flatten_DefinitionMergedBetweenFlattens_KeepsAudienceColumnsAndAddsTheNewQuestion()
    {
        // Arrange
        var expected = PersonalizationGoldenTests.FlattenedInFileOrder();
        var pipeline = PersonalizationPipeline.Compile(_definition);
        pipeline.FlattenAll(_definition, _responses.Take(2));

        // Act
        pipeline.Recompile(_definitionV2, FormSchemaCompileMode.Merge);
        pipeline.FlattenAll(_definitionV2, _responses.Skip(2));

        // Assert
        SortedTypes(pipeline).Should().Equal(SortedTypes(expected));
        var keys = pipeline.ExportKeys();
        keys.Should().ContainInOrder("city", "consent", "age", "AudienceIdentifier", "audience_age");
        CodebookShape(pipeline, "audience_age").Should().Be("personalization");
        CodebookShape(pipeline, "age").Should().Be("scalar");
    }

    [Fact]
    public void Recompile_Replace_DropsAudienceColumnsAndReflattenRestoresExactlyTheUsedOnes()
    {
        // Arrange
        var expected = PersonalizationGoldenTests.FlattenedInFileOrder();
        var pipeline = PersonalizationGoldenTests.FlattenedInFileOrder();

        // Act
        pipeline.Recompile(_definitionV2, FormSchemaCompileMode.Replace);
        var afterReplace = pipeline.PersonalizationColumnTypes();
        pipeline.FlattenAll(_definitionV2, _responses);

        // Assert
        afterReplace.Should().BeEmpty();
        SortedTypes(pipeline).Should().Equal(SortedTypes(expected));
    }

    [Fact]
    public void Flatten_SameResponseAgain_LeavesTheSchemaAsItIs()
    {
        // Arrange
        var pipeline = PersonalizationGoldenTests.FlattenedInFileOrder();
        var before = pipeline.Schema;

        // Act
        pipeline.FlattenAll(_definition, _responses);

        // Assert
        pipeline.Schema.Should().BeSameAs(before);
    }

    [Fact]
    public void Flatten_ResponsesWithoutSnapshots_AddsNoAudienceColumns()
    {
        // Arrange
        var pipeline = PersonalizationPipeline.Compile(_definition);

        // Act
        pipeline.FlattenAll(_definition, _responses.Where(response => response.Snapshot is null));

        // Assert
        pipeline.PersonalizationColumnTypes().Should().BeEmpty();
        pipeline.ExportKeys().Should().NotContain(key => key.StartsWith("audience_") || key == "AudienceIdentifier");
    }

    private static IEnumerable<IReadOnlyList<FixtureResponse>> Permutations(IReadOnlyList<FixtureResponse> items)
    {
        if (items.Count <= 1)
        {
            yield return items;
            yield break;
        }

        for (var first = 0; first < items.Count; first++)
        {
            var rest = items.Where((_, index) => index != first).ToList();
            foreach (var tail in Permutations(rest))
            {
                yield return [items[first], .. tail];
            }
        }
    }

    private static List<(string Key, string DataType)> SortedTypes(PersonalizationPipeline pipeline) =>
        [.. pipeline.PersonalizationColumnTypes().OrderBy(column => column.Key, StringComparer.Ordinal)];

    // A row holds the columns its schema had when it was flattened; only the values it carries are compared.
    private static List<(string Key, string Value)> Values(Dictionary<string, JsonElement?> row) =>
        [.. row
            .Where(entry => entry.Value is not null)
            .Select(entry => (entry.Key, entry.Value!.Value.GetRawText()))
            .OrderBy(entry => entry.Key, StringComparer.Ordinal)];

    private static string? CodebookShape(PersonalizationPipeline pipeline, string columnKey)
    {
        using var codebook = JsonDocument.Parse(pipeline.Schema.CodebookJson);
        return codebook.RootElement.GetProperty("columns").GetProperty(columnKey).GetProperty("exportShape").GetString();
    }
}
