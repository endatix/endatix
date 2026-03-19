using System.Text.Json.Serialization;

namespace Endatix.Core.Authorization.Access;

/// <summary>
/// Authorization data for public form/submission access control
/// </summary>
public class PublicFormAccessData : IAccessData
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

    /// <summary>
    /// The date and time the access data expires.
    /// </summary>
    public DateTimeOffset? ExpiresAt { get; init; }

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
