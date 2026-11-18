using System.Text.Json;

namespace Endatix.Modules.Reporting.Features.FlattenedSubmission;

/// <summary>
/// The value of one ranking column (and a matrix column's position): its 1-based rank, 0 for a choice an answered
/// ranking left unranked, empty when the ranking has no answer. The only writer of a rank cell.
/// </summary>
internal readonly struct RankCell
{
    private readonly int? _rank;

    private RankCell(int rank)
    {
        _rank = rank;
    }

    public static RankCell Empty => default;

    public static RankCell Unranked { get; } = new(0);

    /// <summary>The ranking was answered and this choice left out.</summary>
    public bool IsUnranked => _rank == 0;

    public static RankCell Ranked(int position)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(position, 1);
        return new RankCell(position);
    }

    public JsonElement? ToJson() => _rank is { } rank ? JsonSerializer.SerializeToElement(rank) : null;
}
