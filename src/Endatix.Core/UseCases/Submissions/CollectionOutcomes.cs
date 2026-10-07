using Endatix.Core.Entities;

namespace Endatix.Core.UseCases.Submissions;

/// <summary>Closed collection outcomes a save may request. A normal save omits this.</summary>
public static class CollectionOutcomes
{
    public static bool IsScreenOut(string? outcome) =>
        string.Equals(outcome, CollectionStatusCodes.ScreenOut, StringComparison.Ordinal);

    /// <summary>True when the outcome is omitted or is one this API accepts.</summary>
    public static bool IsSupported(string? outcome) => outcome is null || IsScreenOut(outcome);
}
