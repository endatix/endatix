namespace Endatix.Core.Events;

/// <summary>
/// Identifies which material submission fields changed on a post-completion update.
/// </summary>
[Flags]
public enum SubmissionChangeKind
{
    None = 0,
    Answers = 1,
    Metadata = 2,
    Definition = 4,
    Submitter = 8,
}

/// <summary>
/// Masks used when grouping <see cref="SubmissionChangeKind"/> flags by domain concern.
/// </summary>
public static class SubmissionChangeKindMasks
{
    /// <summary>
    /// Changes that affect the core submission data payload (answers and/or schema alignment).
    /// Distinct from <see cref="SubmissionChangeKind.Metadata"/> and <see cref="SubmissionChangeKind.Submitter"/>.
    /// </summary>
    public const SubmissionChangeKind SubmissionData =
        SubmissionChangeKind.Answers | SubmissionChangeKind.Definition;
}

/// <summary>
/// Extensions for <see cref="SubmissionChangeKind"/>.
/// </summary>
public static class SubmissionChangeKindExtensions
{
    private static readonly (SubmissionChangeKind Kind, string WireName)[] _wireNames =
    [
        (SubmissionChangeKind.Answers, "answers"),
        (SubmissionChangeKind.Metadata, "metadata"),
        (SubmissionChangeKind.Definition, "definition"),
        (SubmissionChangeKind.Submitter, "submitter"),
    ];

    /// <summary>
    /// Determines whether the change affects core submission data (answers or schema definition).
    /// </summary>
    public static bool AffectsSubmissionData(this SubmissionChangeKind changeKind) =>
        (changeKind & SubmissionChangeKindMasks.SubmissionData) != SubmissionChangeKind.None;

    /// <summary>
    /// Parses a wire value into a <see cref="SubmissionChangeKind"/>.
    /// </summary>
    public static SubmissionChangeKind ParseWireValue(string? wireValue)
    {
        if (string.IsNullOrWhiteSpace(wireValue))
        {
            return SubmissionChangeKind.None;
        }

        var parsed = SubmissionChangeKind.None;
        var tokens = wireValue.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        foreach (var token in tokens)
        {
            foreach ((var kind, var wireName) in _wireNames)
            {
                if (string.Equals(token, wireName, StringComparison.OrdinalIgnoreCase))
                {
                    parsed |= kind;
                    break;
                }
            }
        }

        return parsed;
    }

    /// <summary>
    /// Converts the change kind into a comma-separated wire format string.
    /// </summary>
    public static string ToWireValue(this SubmissionChangeKind changeKind)
    {
        if (changeKind == SubmissionChangeKind.None)
        {
            return string.Empty;
        }

        var names = _wireNames
            .Where(entry => changeKind.HasFlag(entry.Kind))
            .Select(entry => entry.WireName);

        return string.Join(',', names);
    }
}
