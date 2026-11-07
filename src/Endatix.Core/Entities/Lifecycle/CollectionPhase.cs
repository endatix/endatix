namespace Endatix.Core.Entities;

/// <summary>
/// Where a submission stands in collection. Every collection status code belongs to one phase, and the phase decides
/// which lifecycle event a change raises and whether the submission can still move.
/// </summary>
public enum CollectionPhase
{
    /// <summary>Not engaged yet: <c>not_started</c>, <c>viewed</c>. Can resume.</summary>
    Pending,

    /// <summary>The respondent is answering: <c>in_progress</c>. Can resume.</summary>
    Active,

    /// <summary>Answered to the end: <c>complete</c>. Final.</summary>
    Completed,

    /// <summary>Ended without completing: <c>screen_out</c>, <c>quota_full</c>, <c>cancelled</c>. Final.</summary>
    Closed,

    /// <summary>Stopped, but can resume: <c>expired</c>, <c>abandoned</c>.</summary>
    Lapsed,
}

/// <summary>Wire names of <see cref="CollectionPhase"/>.</summary>
public static class CollectionPhaseNames
{
    /// <summary>The lowercase wire name, e.g. <c>closed</c>.</summary>
    public static string ToWireName(this CollectionPhase phase) => phase switch
    {
        CollectionPhase.Pending => "pending",
        CollectionPhase.Active => "active",
        CollectionPhase.Completed => "completed",
        CollectionPhase.Closed => "closed",
        CollectionPhase.Lapsed => "lapsed",
        _ => throw new ArgumentOutOfRangeException(nameof(phase), phase, "Unknown collection phase."),
    };

    /// <summary>Parses a wire name, ignoring case.</summary>
    public static bool TryParse(string? name, out CollectionPhase phase)
    {
        var match = Enum.GetValues<CollectionPhase>()
            .Where(candidate => string.Equals(candidate.ToWireName(), name?.Trim(), StringComparison.OrdinalIgnoreCase))
            .Select(candidate => (CollectionPhase?)candidate)
            .FirstOrDefault();
        phase = match.GetValueOrDefault();
        return match.HasValue;
    }
}
