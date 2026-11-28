namespace Endatix.Core.Versioning;

// Keyed moves need the predecessor at this step, not its position in either original array.
internal sealed class SurveySiblingOrder
{
    private readonly LinkedList<string> _order;
    private readonly Dictionary<string, LinkedListNode<string>> _nodes = new(StringComparer.Ordinal);

    internal SurveySiblingOrder(IEnumerable<string> keys)
    {
        _order = new LinkedList<string>();
        foreach (var key in keys)
        {
            _nodes.Add(key, _order.AddLast(key));
        }
    }

    internal SurveyChange[] Move(string path, string key, string? previous)
    {
        if (_nodes[key].Previous?.Value == previous)
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
}
