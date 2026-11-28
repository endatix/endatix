using System.Text.Json;
using System.Text.Json.Nodes;
using Endatix.Core.Versioning;

namespace Endatix.Core.Tests.Versioning;

public sealed class SurveyDiffTests
{
    [Fact]
    public void Compare_RenamedQuestion_RemovesAndAddsByName()
    {
        // Arrange
        const string published = """{"elements":[{"name":"old"}]}""";
        const string draft = """{"elements":[{"name":"new"}]}""";

        // Act
        var changes = SurveyDiff.Compare(published, draft);

        // Assert
        changes.Select(change => (change.Kind, change.Path)).Should().Equal(
            ("removed", "/elements/name:old"), ("added", "/elements/name:new"));
    }

    [Fact]
    public void Compare_GeneratedSurveys_ChangesAndTheirInverseReconstructBothSurveys()
    {
        // Arrange
        var random = new Random(1882);
        for (var sample = 0; sample < 350; sample++)
        {
            var published = Document(random);
            var draft = Document(random);

            // Act
            var changes = SurveyDiff.Compare(published, draft);
            var forward = Apply(JsonNode.Parse(published), changes);
            var restored = Apply(JsonNode.Parse(draft), changes.Reverse().Select(Invert));

            // Assert
            CanonicalJson.Write(forward!.ToJsonString()).Should().Be(draft, $"sample {sample} forward");
            CanonicalJson.Write(restored!.ToJsonString()).Should().Be(published, $"sample {sample} inverse");
        }
    }

    [Theory]
    [InlineData("""{"choices":[1,"1",true]}""", """{"choices":["1",1,true]}""", "moved /choices/value:\"1\"")]
    [InlineData("""{"elements":[{"name":"x"},{"name":"x"}]}""", """{"elements":[{"name":"y"},{"name":"x"}]}""", "changed /elements/index:0/name")]
    [InlineData("""{"pages":[{"name":"p/a~b","title":{"fr":"a"}}]}""", """{"pages":[{"name":"p/a~b","title":{"fr":"b"}}]}""", "changed /pages/name:p~1a~0b/title/fr")]
    [InlineData("""[1]""", """{"pages":[]}""", "changed ")]
    [InlineData("""{"title":"old","title":"new"}""", """{"title":"new"}""", "changed ")]
    [InlineData("""{"x":null}""", """{}""", "removed /x")]
    [InlineData("""{"elements":[{"name":"panel","elements":[{"name":"m","columns":[{"name":"score","title":"A"}]}]}]}""",
        """{"elements":[{"name":"panel","elements":[{"name":"m","columns":[{"name":"score","title":"B"}]}]}]}""",
        "changed /elements/name:panel/elements/name:m/columns/name:score/title")]
    public void Compare_OrderedAndLegacyValues_UsesUnambiguousAddresses(string before, string after, string operation)
    {
        // Act
        var change = SurveyDiff.Compare(before, after).Should().ContainSingle().Subject;

        // Assert
        $"{change.Kind} {change.Path}".Should().Be(operation);
    }

    private static string Document(Random random)
    {
        string[] names = ["one", "One", "1", "x/y~z", "index:0"];
        var pages = names.OrderBy(_ => random.Next()).Take(random.Next(6)).Select(name => new
        {
            name,
            elements = names.OrderBy(_ => random.Next()).Take(random.Next(6)).Select(question => new
            {
                name = random.Next(8) == 0 ? null : question,
                title = new { en = question, fr = random.Next(4).ToString(System.Globalization.CultureInfo.InvariantCulture) },
                choices = Choices(random),
                rows = Choices(random),
                columns = new[] { new { name = question, visible = random.Next(2) == 0 } }
            }).ToArray()
        }).ToArray();
        return CanonicalJson.Write(JsonSerializer.Serialize(new { pages, count = random.Next(4) }));
    }

    [Theory]
    [InlineData(0, 8)]
    [InlineData(8, 0)]
    [InlineData(2, 6)]
    public void Compare_SingleRelocation_CreditsOnlyTheMovedQuestion(int from, int to)
    {
        // Arrange
        var questions = Enumerable.Range(0, 9).Select(i => new { name = $"q{i}" }).ToList();
        var published = JsonSerializer.Serialize(new { elements = questions });
        var moved = questions[from];
        questions.RemoveAt(from);
        questions.Insert(to, moved);
        var draft = JsonSerializer.Serialize(new { elements = questions });

        // Act
        var changes = SurveyDiff.Compare(published, draft);

        // Assert
        var change = changes.Should().ContainSingle().Subject;
        (change.Kind, change.Path).Should().Be(("moved", $"/elements/name:{moved.name}"));
        AssertRoundTrip(published, draft, changes);
    }

    [Theory]
    [InlineData("1", "1.0")]
    [InlineData("0", "-0")]
    [InlineData("9007199254740992", "9007199254740993")]
    [InlineData("0.1", "0.10000000000000001")]
    public void Compare_ChoicesIndistinguishableInJavaScript_AddressesTheEditedLabelByIndex(string first, string second)
    {
        // Arrange
        var published = $$"""{"choices":[{"value":{{first}},"text":"A"},{"value":{{second}},"text":"B"}]}""";
        var draft = published.Replace("\"B\"", "\"Edited\"");

        // Act
        var changes = SurveyDiff.Compare(published, draft);

        // Assert
        var change = changes.Should().ContainSingle().Subject;
        (change.Kind, change.Path).Should().Be(("changed", "/choices/index:1/text"));
        AssertRoundTrip(published, draft, changes);
    }

    private static void AssertRoundTrip(string published, string draft, IReadOnlyList<SurveyChange> changes)
    {
        var forward = Apply(JsonNode.Parse(published), changes);
        var inverse = Apply(JsonNode.Parse(draft), changes.Reverse().Select(Invert));
        forward!.ToJsonString().Should().Be(draft);
        inverse!.ToJsonString().Should().Be(published);
    }

    [Fact]
    public void Compare_AlternateSpellingOfOneUniqueNumericChoice_IsAnEditToTheSameChoice()
    {
        // Arrange
        const string published = """{"choices":[1,"1"]}""";
        const string draft = """{"choices":[1.0,"1"]}""";

        // Act
        var changes = SurveyDiff.Compare(published, draft);

        // Assert
        var change = changes.Should().ContainSingle().Subject;
        (change.Kind, change.Path).Should().Be(("changed", "/choices/value:1"));
        CanonicalSurveyJson.PublishedHash(draft).Should().NotBe(CanonicalSurveyJson.PublishedHash(published));
        AssertRoundTrip(published, draft, changes);
    }

    private static object?[] Choices(Random random)
    {
        object?[] possible = ["1", 1, "1", new { value = true, text = "Yes" }, null, false];
        return possible.OrderBy(_ => random.Next()).Take(random.Next(possible.Length + 1)).ToArray();
    }

    private static SurveyChange Invert(SurveyChange change) => new(
        change.Kind switch { "added" => "removed", "removed" => "added", _ => change.Kind },
        change.Path, change.After, change.Before, change.PredecessorAfter, change.PredecessorBefore);

    private static JsonNode? Apply(JsonNode? root, IEnumerable<SurveyChange> changes)
    {
        foreach (var change in changes)
        {
            var value = change.After is { } after ? JsonNode.Parse(after.GetRawText()) : null;
            if (change.Path.Length == 0)
            {
                root = value;
                continue;
            }
            var parts = change.Path[1..].Split('/').Select(key => key.Replace("~1", "/").Replace("~0", "~")).ToArray();
            var parent = parts.SkipLast(1).Aggregate(root, Descendant);
            ApplyChild(parent!, (parts[^1], change), value);
        }
        return root;
    }

    private static JsonNode? Descendant(JsonNode? parent, string key) =>
        parent is JsonArray array ? array[Position(array, key)] : parent![key];

    private static void ApplyChild(JsonNode parent, (string Key, SurveyChange Change) step, JsonNode? value)
    {
        var (key, change) = step;
        if (parent is JsonArray array)
        {
            ApplyArray(array, step, value);
        }
        else if (change.Kind == "removed")
        {
            parent.AsObject().Remove(key).Should().BeTrue();
        }
        else
        {
            parent[key] = value;
        }
    }

    private static void ApplyArray(JsonArray array, (string Key, SurveyChange Change) step, JsonNode? value)
    {
        var (key, change) = step;
        if (change.Kind is "removed" or "moved")
        {
            var index = Position(array, key);
            value = array[index];
            array.RemoveAt(index);
        }
        if (change.Kind is "added" or "moved")
        {
            var position = key.StartsWith("index:", StringComparison.Ordinal) ? Position(array, key)
                : change.PredecessorAfter is { } anchor ? Position(array, anchor) + 1 : 0;
            array.Insert(position, value);
        }
        if (change.Kind == "changed")
        {
            array[Position(array, key)] = value;
        }
    }

    private static int Position(JsonArray array, string key)
    {
        if (key.StartsWith("index:", StringComparison.Ordinal))
        {
            return int.Parse(key.AsSpan(6), System.Globalization.CultureInfo.InvariantCulture);
        }
        return Enumerable.Range(0, array.Count).Single(i => Matches(array[i], key));
    }

    private static bool Matches(JsonNode? item, string key)
    {
        if (key.StartsWith("name:", StringComparison.Ordinal))
        {
            return item is JsonObject named && named["name"]?.GetValue<string>() == key[5..];
        }
        var value = item is JsonObject choice ? choice["value"] : item;
        var selector = JsonNode.Parse(key[6..])!;
        return value?.GetValueKind() == JsonValueKind.Number && selector.GetValueKind() == JsonValueKind.Number
            ? value.GetValue<double>() == selector.GetValue<double>() : JsonNode.DeepEquals(value, selector);
    }
}
