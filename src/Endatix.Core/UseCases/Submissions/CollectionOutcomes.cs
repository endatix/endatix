using System.Diagnostics.CodeAnalysis;
using Endatix.Core.Entities;

namespace Endatix.Core.UseCases.Submissions;

/// <summary>
/// The collection outcomes a save may request: any registered closed-phase status (<see cref="CollectionStatusCatalog"/>),
/// e.g. <c>screen_out</c> or a host code. A normal save omits it.
/// </summary>
public static class CollectionOutcomes
{
    /// <summary>Returned when a save targets a screened-out submission.</summary>
    public const string SCREENED_OUT_EDIT_REJECTED_MESSAGE = "Screened-out submissions can't be edited yet.";

    /// <summary>Returned when a save targets a submission closed in another outcome.</summary>
    public const string CLOSED_EDIT_REJECTED_MESSAGE = "Closed submissions can't be edited.";

    public static bool IsScreenOut(string? outcome) =>
        string.Equals(outcome, CollectionStatusCodes.ScreenOut, StringComparison.Ordinal);

    /// <summary>True when the outcome is omitted or is a registered closed-phase status.</summary>
    public static bool IsSupported(string? outcome) => outcome is null || TryResolve(outcome, out _);

    /// <summary>The registered closed-phase status an outcome names exactly (lowercase code), or false.</summary>
    public static bool TryResolve(string? outcome, [NotNullWhen(true)] out CollectionStatus? status)
    {
        if (CollectionStatusCatalog.TryGet(outcome, out var registered)
            && registered.Phase == CollectionPhase.Closed
            && string.Equals(registered.Code, outcome, StringComparison.Ordinal))
        {
            status = registered;
            return true;
        }

        status = null;
        return false;
    }
}
