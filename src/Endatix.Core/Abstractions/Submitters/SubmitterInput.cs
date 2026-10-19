namespace Endatix.Core.Abstractions.Submitters;

/// <summary>
/// Input for creating a submitter. <see cref="DisplayId"/> is the label and defaults to the subject.
/// </summary>
public sealed record SubmitterInput(
    string ExternalSubjectId,
    string? DisplayId = null,
    string? AuthProvider = null,
    long? AppUserId = null,
    Dictionary<string, string>? Profile = null);
