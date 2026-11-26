using Endatix.Core.Abstractions.Authorization;
using Endatix.IntegrationTests.Shared;

namespace Endatix.IntegrationTests;

internal static class TenantUserManagerPersona
{
    internal static async Task<HttpClient> UserManagerClientAsync(this IntegrationTestWorld world, CancellationToken ct)
    {
        var tenant = world.Tenants[0].Id;
        const string role = "UserManagerOnly";
        const string name = "user-manager";
        await world.Seed.SeedRoleAsync(tenant, role, ct);
        await world.Seed.SeedRolePermissionsAsync(tenant, role, [Actions.Access.Hub, Actions.Tenant.ViewUsers, Actions.Tenant.ManageUsers], ct);
        await world.Seed.SeedUserAsync(tenant, name, $"{name}@test.local", cancellationToken: ct);
        await world.Seed.SeedUserRoleAssignmentAsync(tenant, name, role, ct);
        return await world.AsAsync(TestPersona.CustomRole(role), mode: IntegrationAuthMode.SyntheticJwt, userName: name, cancellationToken: ct);
    }
}
