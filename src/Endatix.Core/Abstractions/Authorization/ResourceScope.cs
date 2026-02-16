namespace Endatix.Core.Abstractions.Authorization;

/// <summary>
/// A scope of a resource with its type, id and permissions.
/// </summary>
/// <param name="ResourceType">The type of the resource</param>
/// <param name="ResourceId">The id of the resource</param>
/// <param name="Permissions">The permissions of the resource</param>
public sealed record ResourceScope(
    string ResourceType,
    string ResourceId,
    string[] Permissions
);