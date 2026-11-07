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

    /// <summary>Returned when a save names an outcome that is not a registered closed status.</summary>
    public const string UNSUPPORTED_MESSAGE = "The collection outcome is not a registered closed status.";

    public static bool IsScreenOut(string? outcome) =>
        string.Equals(outcome, CollectionStatusCodes.ScreenOut, StringComparison.Ordinal);

    /// <summary>True when the outcome is omitted or is a registered closed-phase status.</summary>
    public static bool IsSupported(string? outcome) => IsSupported(outcome, out _);

    /// <summary>True when the outcome is omitted (<paramref name="status"/> null) or names a registered closed status.</summary>
    public static bool IsSupported(string? outcome, out CollectionStatus? status)
    {
        status = null;
        return outcome is null || TryResolve(outcome, out status);
    }

    /// <summary>Why a save of a closed submission was refused, or null when the submission was not closed.</summary>
    public static string? RejectedEditMessage(CollectionStatus previous) => previous switch
    {
        { Code: CollectionStatusCodes.ScreenOut } => SCREENED_OUT_EDIT_REJECTED_MESSAGE,
        { Phase: CollectionPhase.Closed } => CLOSED_EDIT_REJECTED_MESSAGE,
        _ => null,
    };

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
