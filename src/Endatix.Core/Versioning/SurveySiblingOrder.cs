namespace Endatix.Core.Versioning;

// Keyed moves need the predecessor at this step, not its position in either original array.
internal sealed class SurveySiblingOrder
{
    private readonly LinkedList<string> _order;
    private readonly Dictionary<string, LinkedListNode<string>> _nodes = new(StringComparer.Ordinal);
    private readonly HashSet<string> _stationary;

    internal SurveySiblingOrder(IReadOnlyCollection<string> keys, IEnumerable<string> target)
    {
        _stationary = StationaryKeys(keys, target);
        _order = new LinkedList<string>();
        foreach (var key in keys)
        {
            _nodes.Add(key, _order.AddLast(key));
        }
    }

    internal SurveyChange[] Move(string path, string key, string? previous)
    {
        if (_stationary.Contains(key) || _nodes[key].Previous?.Value == previous)
        {
            return [];
        }
        var from = Remove(key);
        Insert(key, previous);
        return [new("moved", path, PredecessorBefore: from, PredecessorAfter: previous)];
    }

    internal string? Remove(string key)
    {
        var node = _nodes[key];
        var previous = node.Previous?.Value;
        _order.Remove(node);
        _nodes.Remove(key);
        return previous;
    }

    internal void Insert(string key, string? previous) =>
        _nodes.Add(key, previous is null ? _order.AddFirst(key) : _order.AddAfter(_nodes[previous], key));

    // Keep a longest common subsequence. With unique keys this is the longest increasing
    // subsequence of original positions in target order; only the other survivors moved.
    private static HashSet<string> StationaryKeys(IEnumerable<string> original, IEnumerable<string> target)
    {
        var positions = original.Index().ToDictionary(pair => pair.Item, pair => pair.Index, StringComparer.Ordinal);
        var survivors = target.Where(positions.ContainsKey).ToArray();
        var tails = new List<int>();
        var last = new int[survivors.Length];
        var predecessor = new int[survivors.Length];
        for (var i = 0; i < survivors.Length; i++)
        {
            var length = PlaceTail(tails, positions[survivors[i]]);
            predecessor[i] = length == 0 ? -1 : last[length - 1];
            last[length] = i;
        }
        var stationary = new HashSet<string>(StringComparer.Ordinal);
        for (var i = tails.Count == 0 ? -1 : last[tails.Count - 1]; i >= 0; i = predecessor[i])
        {
            stationary.Add(survivors[i]);
        }
        return stationary;
    }

    private static int PlaceTail(List<int> tails, int position)
    {
        var slot = ~tails.BinarySearch(position);
        if (slot == tails.Count)
        {
            tails.Add(position);
        }
        else
        {
            tails[slot] = position;
        }
        return slot;
    }
}
