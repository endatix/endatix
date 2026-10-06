using Endatix.Core.Entities;

namespace Endatix.Core.UseCases.Submissions;

/// <summary>Closed completion outcomes a public save may request. Partial saves omit this.</summary>
public static class CollectionOutcomes
{
    public static bool IsScreenOut(string? outcome) =>
        string.Equals(outcome, CollectionStatusCodes.ScreenOut, StringComparison.Ordinal);
}
