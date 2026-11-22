namespace Endatix.Core.Versioning;

/// <summary>When a version is published, and by whom.</summary>
/// <param name="At">The time of the publish, in UTC.</param>
/// <param name="By">The user publishing, when known.</param>
internal readonly record struct ContentVersionStamp(DateTime At, long? By);
