namespace Endatix.Core.Abstractions.Submitters;

/// <summary>
/// Input for extracting a submitter from a claims principal.
/// </summary>
public sealed record SubmitterExtractionInput(
    string AuthProvider,
    string? ExternalSubjectId,
    string? DisplayId,
    long? AppUserId,
    IReadOnlyDictionary<string, string>? Profile = null);
