using System.Text.Json;

namespace Endatix.Modules.Reporting.Features.FormSchema.Choices;

/// <summary>
/// The value of one choice indicator column: 1 shown and picked, 0 shown and not picked, empty when the respondent
/// was not shown the choice or it cannot be known. The only writer of an indicator cell.
/// </summary>
internal readonly struct IndicatorCell
{
    private static readonly JsonElement _one = JsonSerializer.SerializeToElement(1);
    private static readonly JsonElement _zero = JsonSerializer.SerializeToElement(0);

    private readonly State _state;

    private IndicatorCell(State state)
    {
        _state = state;
    }

    private enum State : byte
    {
        Empty,
        Picked,
        ShownNotPicked,
    }

    public static IndicatorCell Empty => default;

    /// <summary>A picked choice was shown.</summary>
    public static IndicatorCell Picked { get; } = new(State.Picked);

    /// <summary>Only for a choice the respondent provably saw.</summary>
    public static IndicatorCell ShownNotPicked { get; } = new(State.ShownNotPicked);

    public JsonElement? ToJson() =>
        _state switch
        {
            State.Picked => _one,
            State.ShownNotPicked => _zero,
            _ => null,
        };
}
