namespace Endatix.Core.Abstractions.Submitters;

/// <summary>
/// Input for creating a submitter.
/// </summary>  
public sealed record SubmitterInput(
    string ExternalSubjectId,
    string DisplayId,
    string? AuthProvider = null,
    long? AppUserId = null,
    Dictionary<string, string>? Profile = null);
