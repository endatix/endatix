namespace Endatix.Core.Abstractions.Submitters;

/// <summary>
/// Input for creating a submitter. <see cref="DisplayId"/> is the label: when omitted, a new submitter is
/// labelled with the subject and an existing one keeps its label.
/// </summary>
public sealed record SubmitterInput(
    string ExternalSubjectId,
    string? DisplayId = null,
    string? AuthProvider = null,
    long? AppUserId = null,
    Dictionary<string, string>? Profile = null);
