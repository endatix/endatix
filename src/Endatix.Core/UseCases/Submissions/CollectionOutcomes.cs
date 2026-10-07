using Endatix.Core.Entities;

namespace Endatix.Core.UseCases.Submissions;

/// <summary>Closed collection outcomes a save may request. A normal save omits this.</summary>
public static class CollectionOutcomes
{
    /// <summary>Returned when a save targets a screened-out submission.</summary>
    public const string SCREENED_OUT_EDIT_REJECTED_MESSAGE = "Screened-out submissions can't be edited yet.";

    public static bool IsScreenOut(string? outcome) =>
        string.Equals(outcome, CollectionStatusCodes.ScreenOut, StringComparison.Ordinal);

    /// <summary>True when the outcome is omitted or is one this API accepts.</summary>
    public static bool IsSupported(string? outcome) => outcome is null || IsScreenOut(outcome);
}
