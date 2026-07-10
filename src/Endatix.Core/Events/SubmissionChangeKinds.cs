namespace Endatix.Core.Events;

/// <summary>
/// Identifies which material submission fields changed on a post-completion update.
/// </summary>
[Flags]
public enum SubmissionChangeKinds
{
    None = 0,
    Answers = 1,
    Metadata = 2,
    Definition = 4,
    Submitter = 8,
}

/// <summary>
/// Masks used when grouping <see cref="SubmissionChangeKinds"/> flags by domain concern.
/// </summary>
public static class SubmissionChangeKindsMasks
{
    /// <summary>
    /// Changes that affect the core submission data payload (answers and/or schema alignment).
    /// Distinct from <see cref="SubmissionChangeKinds.Metadata"/> and <see cref="SubmissionChangeKinds.Submitter"/>.
    /// </summary>
    public const SubmissionChangeKinds SubmissionData =
        SubmissionChangeKinds.Answers | SubmissionChangeKinds.Definition;
}

/// <summary>
/// Extensions for <see cref="SubmissionChangeKinds"/>.
/// </summary>
public static class SubmissionChangeKindsExtensions
{
    private static readonly (SubmissionChangeKinds Kind, string WireName)[] _wireNames =
    [
        (SubmissionChangeKinds.Answers, "answers"),
        (SubmissionChangeKinds.Metadata, "metadata"),
        (SubmissionChangeKinds.Definition, "definition"),
        (SubmissionChangeKinds.Submitter, "submitter"),
    ];

    /// <summary>
    /// Determines whether the change affects core submission data (answers or schema definition).
    /// </summary>
    public static bool AffectsSubmissionData(this SubmissionChangeKinds changeKinds) =>
        (changeKinds & SubmissionChangeKindsMasks.SubmissionData) != SubmissionChangeKinds.None;

    /// <summary>
    /// Parses a wire value into a <see cref="SubmissionChangeKinds"/>.
    /// </summary>
    public static SubmissionChangeKinds ParseWireValue(string? wireValue)
    {
        if (string.IsNullOrWhiteSpace(wireValue))
        {
            return SubmissionChangeKinds.None;
        }

        var parsed = SubmissionChangeKinds.None;
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
    /// Converts the change kinds into a comma-separated wire format string.
    /// </summary>
    public static string ToWireValue(this SubmissionChangeKinds changeKinds)
    {
        if (changeKinds == SubmissionChangeKinds.None)
        {
            return string.Empty;
        }

        var names = _wireNames
            .Where(entry => changeKinds.HasFlag(entry.Kind))
            .Select(entry => entry.WireName);

        return string.Join(',', names);
    }
}
