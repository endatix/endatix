using System.Text.Json;
using Endatix.Modules.Reporting.Features.SearchCopy;
using Endatix.Modules.Reporting.Features.SubmissionGrid;

namespace Endatix.Modules.Reporting.Tests.Features.SearchCopy;

public sealed class SearchCopyDocumentTests
{
    private static readonly DateTime AddedAt = new(2026, 10, 16, 0, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Build_AnsweredSearchKeys_CopiesThemNormalised()
    {
        // Arrange
        var flattened = Flattened("""{"company":"Acme Corp","visit":"2026-03-01T10:30","born":"2001-02-03"}""");
        var keys = Keys(
            ("company", SubmissionGridFilterTypes.Text),
            ("visit", SubmissionGridFilterTypes.DateTime),
            ("born", SubmissionGridFilterTypes.Date));

        // Act
        var searchJson = SearchCopyDocument.Build(flattened, keys);

        // Assert
        searchJson.Should().Be("""{"company":"acme corp","visit":"2026-03-01T10:30:00","born":"2001-02-03"}""");
    }

    [Fact]
    public void Build_UnansweredOrMissingKey_LeavesItOut()
    {
        // Arrange
        var flattened = Flattened("""{"company":null,"city":"Sofia"}""");
        var keys = Keys(("company", SubmissionGridFilterTypes.Text), ("city", SubmissionGridFilterTypes.Text), ("gone", SubmissionGridFilterTypes.Text));

        // Act
        var searchJson = SearchCopyDocument.Build(flattened, keys);

        // Assert
        searchJson.Should().Be("""{"city":"sofia"}""");
    }

    [Fact]
    public void Build_ColumnThatIsNotASearchKey_LeavesItOut()
    {
        // Arrange
        var flattened = Flattened("""{"company":"Acme","notes":"Private"}""");
        var keys = Keys(("company", SubmissionGridFilterTypes.Text));

        // Act
        var searchJson = SearchCopyDocument.Build(flattened, keys);

        // Assert
        searchJson.Should().Be("""{"company":"acme"}""");
    }

    [Fact]
    public void Build_NumberOrBoolean_CopiesItsJsonText()
    {
        // Arrange
        var flattened = Flattened("""{"choice":2.50,"agreed":true}""");
        var keys = Keys(("choice", SubmissionGridFilterTypes.Text), ("agreed", SubmissionGridFilterTypes.Text));

        // Act
        var searchJson = SearchCopyDocument.Build(flattened, keys);

        // Assert
        searchJson.Should().Be("""{"choice":"2.50","agreed":"true"}""");
    }

    [Fact]
    public void Build_ArrayOrObject_LeavesItOut()
    {
        // Arrange
        var flattened = Flattened("""{"colors":["red"],"address":{"city":"Sofia"},"company":"Acme"}""");
        var keys = Keys(("colors", SubmissionGridFilterTypes.Text), ("address", SubmissionGridFilterTypes.Text), ("company", SubmissionGridFilterTypes.Text));

        // Act
        var searchJson = SearchCopyDocument.Build(flattened, keys);

        // Assert
        searchJson.Should().Be("""{"company":"acme"}""");
    }

    private static Dictionary<string, JsonElement?> Flattened(string json)
    {
        using var document = JsonDocument.Parse(json);
        return document.RootElement.EnumerateObject()
            .ToDictionary(property => property.Name, property => (JsonElement?)property.Value.Clone(), StringComparer.Ordinal);
    }

    private static FormSearchKeys Keys(params (string Key, string FilterType)[] keys) =>
        FormSearchKeys.FromJson(JsonSerializer.Serialize(
            keys.Select(key => new SearchKey(key.Key, SearchKeySources.Auto, key.FilterType, AddedAt)),
            new JsonSerializerOptions(JsonSerializerDefaults.Web)));
}
