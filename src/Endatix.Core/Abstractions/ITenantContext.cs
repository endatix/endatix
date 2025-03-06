namespace Endatix.Core.Abstractions;

/// <summary>
/// Provides access to the current tenant context within a request scope
/// </summary>
public interface ITenantContext
{
    /// <summary>
    /// Gets the current tenant ID
    /// </summary>
    long? TenantId { get; }
}
