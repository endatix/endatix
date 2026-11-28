using System.Globalization;
using System.Text.Json;

namespace Endatix.Core.Versioning;

internal sealed class SurveyDiff
{
    private readonly List<SurveyChange> _changes = [];

    // Both documents have already passed through the survey canonical writer.
    internal static IReadOnlyList<SurveyChange> Compare(string published, string draft)
    {
        if (published == draft)
        {
            return [];
        }
        var diff = new SurveyDiff();
        diff.Visit("", (JsonSerializer.Deserialize<JsonElement>(published), JsonSerializer.Deserialize<JsonElement>(draft)), "");
        return diff._changes;
    }

    private void Visit(string path, (JsonElement Before, JsonElement After) pair, string property)
    {
        var (before, after) = pair;
        if (before.ValueKind == JsonValueKind.Object && after.ValueKind == JsonValueKind.Object)
        {
            CompareObject(path, before, after);
        }
        else if (before.ValueKind == JsonValueKind.Array && after.ValueKind == JsonValueKind.Array)
        {
            CompareArray(path, pair, property);
        }
        else if (before.GetRawText() != after.GetRawText())
        {
            _changes.Add(new("changed", path, before, after));
        }
    }

    private void CompareObject(string path, JsonElement before, JsonElement after)
    {
        var source = Properties(before);
        var target = Properties(after);
        if (source is null || target is null)
        {
            // Duplicate legacy properties cannot be addressed separately.
            if (before.GetRawText() != after.GetRawText())
            {
                _changes.Add(new("changed", path, before, after));
            }
            return;
        }
        foreach (var name in source.Keys.Union(target.Keys).Order(StringComparer.Ordinal))
        {
            CompareProperty(Child(path, name), name, (source, target));
        }
    }

    private static Dictionary<string, JsonElement>? Properties(JsonElement element)
    {
        var properties = new Dictionary<string, JsonElement>(StringComparer.Ordinal);
        var unique = element.EnumerateObject().All(property => properties.TryAdd(property.Name, property.Value));
        return unique ? properties : null;
    }

    private void CompareProperty(string path, string name, (Dictionary<string, JsonElement> Before, Dictionary<string, JsonElement> After) parents)
    {
        var had = parents.Before.TryGetValue(name, out var before);
        var has = parents.After.TryGetValue(name, out var after);
        if (!had)
        {
            _changes.Add(new("added", path, After: after));
        }
        else if (!has)
        {
            _changes.Add(new("removed", path, Before: before));
        }
        else
        {
            Visit(path, (before, after), name);
        }
    }

    private void CompareArray(string path, (JsonElement Before, JsonElement After) pair, string property)
    {
        var before = SurveyArrayIdentity.Index(pair.Before, property);
        var after = SurveyArrayIdentity.Index(pair.After, property);
        if (before is null || after is null)
        {
            CompareByIndex(path, pair.Before.EnumerateArray().ToArray(), pair.After.EnumerateArray().ToArray());
            return;
        }
        var order = new SurveySiblingOrder(before.Keys, after.Keys);
        foreach (var key in before.Keys.Except(after.Keys))
        {
            _changes.Add(new("removed", Child(path, key), before[key], PredecessorBefore: order.Remove(key)));
        }
        CompareMembers(path, (before, after), order);
    }

    private void CompareMembers(string path, (Dictionary<string, JsonElement> Before, Dictionary<string, JsonElement> After) items,
        SurveySiblingOrder order)
    {
        string? previous = null;
        foreach (var (key, value) in items.After)
        {
            var child = Child(path, key);
            if (items.Before.TryGetValue(key, out var original))
            {
                _changes.AddRange(order.Move(child, key, previous));
                Visit(child, (original, value), "");
            }
            else
            {
                order.Insert(key, previous);
                _changes.Add(new("added", child, After: value, PredecessorAfter: previous));
            }
            previous = key;
        }
    }

    private void CompareByIndex(string path, JsonElement[] before, JsonElement[] after)
    {
        var common = Math.Min(before.Length, after.Length);
        for (var i = 0; i < common; i++)
        {
            Visit(IndexPath(path, i), (before[i], after[i]), "");
        }
        for (var i = before.Length - 1; i >= common; i--)
        {
            _changes.Add(new("removed", IndexPath(path, i), Before: before[i]));
        }
        for (var i = common; i < after.Length; i++)
        {
            _changes.Add(new("added", IndexPath(path, i), After: after[i]));
        }
    }

    internal static string IndexPath(string path, int index) => $"{path}/index:{index.ToString(CultureInfo.InvariantCulture)}";
    internal static string Child(string path, string segment) => $"{path}/{segment.Replace("~", "~0").Replace("/", "~1")}";
}
