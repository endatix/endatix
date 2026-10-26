using System.Text.Json;
using Endatix.Modules.Audience.Contracts;
using Endatix.Modules.Audience.Domain;

namespace Endatix.Modules.Audience.Features.Import;

/// <summary>
/// The choice columns of one row: one key per single-choice property, a list per multi-choice property, and the
/// Other text. A cell ticks with <c>1</c>, <c>true</c>, <c>yes</c>, <c>y</c> or <c>x</c> and stays unticked with
/// <c>0</c>, <c>false</c>, <c>no</c>, <c>n</c> or nothing; any other value is an error, never a guess.
/// </summary>
internal sealed class ChoiceCells(Dictionary<long, string> values)
{
    private static readonly HashSet<string> TickedCells = new(["1", "true", "yes", "y", "x"], StringComparer.OrdinalIgnoreCase);
    private static readonly HashSet<string> UntickedCells = new(["", "0", "false", "no", "n"], StringComparer.OrdinalIgnoreCase);

    private readonly Dictionary<long, List<string>> _multi = new();
    private readonly Dictionary<long, OtherCells> _others = new();
    private readonly HashSet<long> _single = [];
    private string? _error;

    public void Read(string header, ChoiceColumn column, string cell)
    {
        string trimmed = cell.Trim();
        if (column.Role == ChoiceColumnRole.OtherText)
        {
            OthersOf(column.Property).ReadText(header, trimmed);
            return;
        }

        if (IsTicked(trimmed) is not { } ticked)
        {
            _error ??= $"'{header}' must be 1 or 0 (true/false, yes/no, y/n and x also work), not '{trimmed}'.";
            return;
        }

        ReadTick(header, column, ticked);
    }

    public CellRead Finish(JsonSerializerOptions json)
    {
        foreach (OtherCells other in _others.Values)
        {
            string? otherError = other.Error();
            _error ??= otherError;
            if (otherError is null && other.Text.Length > 0)
            {
                AddMulti(other.Property.Id, other.Text);
            }
        }

        foreach ((long propertyId, List<string> keys) in _multi)
        {
            values[propertyId] = JsonSerializer.Serialize(keys, json);
        }

        return new CellRead(values, _error);
    }

    private static bool? IsTicked(string cell)
    {
        if (TickedCells.Contains(cell))
        {
            return true;
        }

        return UntickedCells.Contains(cell) ? false : null;
    }

    private void ReadTick(string header, ChoiceColumn column, bool ticked)
    {
        if (column.Role == ChoiceColumnRole.Other)
        {
            OthersOf(column.Property).ReadTick(header, ticked, column.ChoiceKey is null);
        }

        if (ticked && column.ChoiceKey is { } key)
        {
            Tick(column.Property, key);
        }
    }

    private void Tick(Property property, string key)
    {
        if (property.DataType == AudienceDataTypeCodes.MultipleChoice)
        {
            AddMulti(property.Id, key);
            return;
        }

        if (!_single.Add(property.Id))
        {
            _error ??= $"'{property.Name}' takes one choice, but the row marks more than one.";
        }

        values[property.Id] = key;
    }

    private void AddMulti(long propertyId, string key)
    {
        if (!_multi.TryGetValue(propertyId, out List<string>? keys))
        {
            keys = [];
            _multi[propertyId] = keys;
        }

        if (!keys.Contains(key, StringComparer.Ordinal))
        {
            keys.Add(key);
        }
    }

    private OtherCells OthersOf(Property property)
    {
        if (!_others.TryGetValue(property.Id, out OtherCells? other))
        {
            other = new OtherCells(property);
            _others[property.Id] = other;
        }

        return other;
    }

    /// <summary>
    /// A multi-choice property's <c>__other</c> tick and <c>__other_text</c> cell. The text is stored only when the
    /// property allows Other and <c>__other</c> is ticked; a ticked <c>__other</c> that stands for no listed key needs
    /// the text.
    /// </summary>
    private sealed class OtherCells(Property property)
    {
        private string? _tickHeader;
        private string? _textHeader;
        private bool _ticked;
        private bool _needsText;

        public Property Property => property;

        public string Text { get; private set; } = string.Empty;

        public void ReadTick(string header, bool ticked, bool needsText)
        {
            _tickHeader = header;
            _ticked = ticked;
            _needsText = needsText;
        }

        public void ReadText(string header, string text)
        {
            _textHeader = header;
            Text = text;
        }

        public string? Error()
        {
            if (Text.Length == 0)
            {
                return _ticked && _needsText
                    ? $"'{_tickHeader}' is ticked, but '{_tickHeader}_text' is empty."
                    : null;
            }

            if (!property.AllowsOther)
            {
                return $"'{_textHeader}' has text, but '{property.Name}' does not allow other choices.";
            }

            return _ticked ? null : $"'{_textHeader}' has text, but '{TickHeader()}' is not ticked.";
        }

        private string TickHeader() => _tickHeader ?? _textHeader![..^"_text".Length];
    }
}
