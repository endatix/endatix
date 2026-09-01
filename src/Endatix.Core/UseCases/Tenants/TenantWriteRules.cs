using Endatix.Core.Infrastructure.Result;

namespace Endatix.Core.UseCases.Tenants;

/// <summary>
/// Write rules shared by the tenant create and update use cases.
/// </summary>
public static class TenantWriteRules
{
    public static ValidationError InvalidName(string identifier) => new()
    {
        Identifier = identifier,
        ErrorMessage = "Tenant name cannot be empty."
    };
}
