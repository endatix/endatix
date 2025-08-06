namespace Endatix.Core.Abstractions;

public interface ITenantOwned
{
    long TenantId { get; }
} 