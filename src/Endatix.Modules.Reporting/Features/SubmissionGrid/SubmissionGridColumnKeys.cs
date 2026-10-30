namespace Endatix.Modules.Reporting.Features.SubmissionGrid;

/// <summary>
/// The <c>columns</c> parameter of the grid's submissions list: comma-separated answer column keys.
/// </summary>
public static class SubmissionGridColumnKeys
{
    /// <summary>The most columns one request can ask for.</summary>
    public const int MaxColumns = 500;

    /// <summary>The longest column key accepted.</summary>
    public const int MaxKeyLength = 512;

    /// <summary>
    /// The distinct, trimmed keys in request order; empty when <paramref name="columns"/> is empty.
    /// </summary>
    /// <remarks>
    /// A key that contains a comma cannot be requested; see hub#1023.
    /// </remarks>
    public static IReadOnlyList<string> Parse(string? columns) =>
        string.IsNullOrWhiteSpace(columns)
            ? []
            : columns
                .Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
                .Distinct(StringComparer.Ordinal)
                .ToList();
}
