using Ardalis.GuardClauses;

namespace Endatix.Core.Entities;

/// <summary>
/// Shared domain guards for submission-related invariants. Prefer adding new checks here
/// so call sites stay consistent as validation rules grow.
/// </summary>
public static class SubmissionGuards
{
    /// <summary>
    /// When <paramref name="submissionTokenExpiryHours"/> is set, it must be positive.
    /// <c>null</c> is allowed (inherit / no override).
    /// </summary>
    public static void AgainstInvalidSubmissionTokenExpiry(int? submissionTokenExpiryHours)
    {
        if (submissionTokenExpiryHours.HasValue)
        {
            Guard.Against.NegativeOrZero(
                submissionTokenExpiryHours.Value);
        }
    }
}
