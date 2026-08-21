using Endatix.Core.Infrastructure.Result;
using Entities = Endatix.Core.Entities;

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

    public static ValidationError ForbiddenRegistrationRole(string roleName, string identifier) => new()
    {
        Identifier = identifier,
        ErrorMessage = $"Default registration role '{roleName}' is not allowed. Use a persisted tenant role (default: {Entities.TenantSettings.DefaultRegistrationRole})."
    };
}
