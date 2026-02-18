using System.Text.Json.Serialization;

namespace Endatix.Core.Abstractions.Authorization;

/// <summary>
/// Authorization data for form/submission access with O(1) permission checks.
/// </summary>
public class SubmissionAccessData : IAccessData
{
    /// <inheritdoc/>
    [JsonIgnore]
    public HashSet<string> Permissions => FormPermissions.Union(SubmissionPermissions).ToHashSet();

    /// <summary>
    /// The form ID this access data applies to.
    /// </summary>
    public string FormId { get; init; } = string.Empty;

    /// <summary>
    /// The submission ID if applicable (null for new submissions).
    /// </summary>
    public string? SubmissionId { get; init; }

    /// <summary>
    /// Permissions for the form resource.
    /// </summary>
    public HashSet<string> FormPermissions { get; init; } = [];

    /// <summary>
    /// Permissions for the submission resource (or "new" submission when no submissionId provided).
    /// </summary>
    public HashSet<string> SubmissionPermissions { get; init; } = [];

    /// <inheritdoc/>
    public bool Has(string permission)
    {
        return Permissions.Contains(permission);
    }

    /// <inheritdoc/>
    public bool HasAny(IEnumerable<string> permissions)
    {
        return permissions.Any(Has);
    }

    /// <inheritdoc/>
    public bool HasAll(IEnumerable<string> permissions)
    {
        return permissions.All(Has);
    }
}
