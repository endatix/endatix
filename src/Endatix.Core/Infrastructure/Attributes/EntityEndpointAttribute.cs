using Ardalis.GuardClauses;

namespace Endatix.Core.Infrastructure.Attributes;

/// <summary>
/// Attribute to specify entity information for endpoints that support ownership-based authorization.
/// Combines entity type and route parameter name for the entity ID.
/// </summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = true)]
public sealed class EntityEndpointAttribute : Attribute
{
    /// <summary>
    /// Gets the entity type this endpoint operates on.
    /// </summary>
    public Type EntityType { get; }

    /// <summary>
    /// Gets the name of the route parameter that contains the entity ID.
    /// </summary>
    public string EntityIdRoute { get; }

    /// <summary>
    /// Initializes a new instance of the EntityEndpointAttribute.
    /// </summary>
    /// <param name="entityType">The entity type this endpoint operates on</param>
    /// <param name="entityIdRoute">The name of the route parameter that contains the entity ID (e.g., "submissionId")</param>
    public EntityEndpointAttribute(Type entityType, string entityIdRoute)
    {
        Guard.Against.Null(entityType);
        Guard.Against.NullOrWhiteSpace(entityIdRoute);

        EntityType = entityType;
        EntityIdRoute = entityIdRoute;
    }
}