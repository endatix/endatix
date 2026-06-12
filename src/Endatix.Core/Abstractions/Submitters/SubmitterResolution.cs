namespace Endatix.Core.Abstractions.Submitters;

/// <summary>
/// Resolution of a submitter from a claims principal.
/// </summary>
public sealed record SubmitterResolution(
    long? SubmitterId,
    string? DisplayId,
    string? ProfileSnapshot);
