using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using Endatix.Core.Abstractions;
using Endatix.Core.Abstractions.Authorization;
using Endatix.Core.Infrastructure.Result;
using Endatix.Infrastructure.Identity;
using Endatix.Infrastructure.Identity.Authorization.Data;
using Endatix.Infrastructure.Multitenancy;
using Endatix.IntegrationTests.Shared;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Endatix.IntegrationTests.FeatureFlows.Identity;

/// <summary>
/// A user who may manage roles or invite users can only hand out access they hold themselves: creating, changing,
/// assigning, removing or inviting with a role beyond the caller's own permissions is forbidden, the built-in Admin
/// role included. Admin is recognized by the built-in system role, not by a role's name.
/// </summary>
[Collection(nameof(EndatixIntegrationTestCollection))]
[Trait("Category", "FeatureFlow")]
[Trait("Priority", "P0")]
public sealed class RoleGrantCeilingFlowTests(EndatixIntegrationWebHostFixture fixture)
{
    private const string SEED_PASSWORD = "Password123!";
    private const string MANAGER_ROLE = "RoleManagerOnly";
    private const string MANAGER_USER = "role-manager";
    private const string CATALOG_ROLE = "CatalogHolder";
    private const string NARROW_ROLE = "HubOnly";
    private const string SHARED_ROLE = "Reviewer";
    private const string CEILING_MESSAGE = "You can only grant access you hold yourself.";
    private const string OWN_ACCESS_MESSAGE = "You cannot change your own access.";

    private static readonly string[] _managerPermissions = [Actions.Access.Hub, Actions.Tenant.ManageRoles];

    private static readonly string[] _beyondManager =
        [Actions.Tenant.ViewUsers, Actions.Tenant.ManageUsers, Actions.Tenant.ManageSettings];

    [Fact]
    public async Task A_role_manager_cannot_create_a_role_beyond_their_own_permissions()
    {
        var ct = TestContext.Current.CancellationToken;
        var (world, _) = await PrepareAsync(ct);
        using var manager = await ManagerClientAsync(world, ct);

        using var response = await manager.PostAsJsonAsync(
            "/api/roles",
            new { name = "Wider", permissions = _beyondManager.Append(Actions.Tenant.ManageRoles).ToArray() },
            ct);

        await ShouldBeRefusedByTheCeilingAsync(response, ct);
    }

    [Fact]
    public async Task A_role_manager_can_create_a_role_within_their_own_permissions()
    {
        var ct = TestContext.Current.CancellationToken;
        var (world, _) = await PrepareAsync(ct);
        using var manager = await ManagerClientAsync(world, ct);

        using var response = await manager.PostAsJsonAsync(
            "/api/roles",
            new { name = "Within", permissions = new[] { Actions.Access.Hub } },
            ct);

        response.StatusCode.Should().Be(HttpStatusCode.Created);
    }

    [Fact]
    public async Task A_tenant_admin_can_create_a_role_with_any_tenant_permission()
    {
        var ct = TestContext.Current.CancellationToken;
        var (world, _) = await PrepareAsync(ct);
        using var admin = await world.AsAsync(TestPersona.TenantAdmin, cancellationToken: ct);

        using var response = await admin.PostAsJsonAsync(
            "/api/roles",
            new { name = "Wider", permissions = _beyondManager.Append(Actions.Tenant.ManageRoles).ToArray() },
            ct);

        response.StatusCode.Should().Be(HttpStatusCode.Created);
    }

    [Fact]
    public async Task A_tenant_role_cannot_carry_a_platform_permission()
    {
        var ct = TestContext.Current.CancellationToken;
        var (world, _) = await PrepareAsync(ct);
        await world.Seed.SeedRolePermissionsAsync(world.Tenants[0].Id, CATALOG_ROLE, [Actions.Platform.ManageTenants], ct);
        using var admin = await world.AsAsync(TestPersona.TenantAdmin, cancellationToken: ct);

        using var created = await admin.PostAsJsonAsync(
            "/api/roles",
            new { name = "Platformish", permissions = new[] { Actions.Access.Hub, Actions.Platform.ManageTenants } },
            ct);
        using var updated = await admin.PutAsJsonAsync(
            $"/api/roles/{NARROW_ROLE}",
            new { permissions = new[] { Actions.Access.Hub, Actions.Platform.ManageTenants } },
            ct);

        created.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        updated.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Theory]
    [InlineData("PlatformAdmin")]
    [InlineData("platformadmin")]
    [InlineData(" PLATFORMADMIN ")]
    [InlineData("Admin")]
    [InlineData("admin")]
    [InlineData("Creator")]
    public async Task A_tenant_cannot_create_a_role_with_a_system_roles_name(string name)
    {
        var ct = TestContext.Current.CancellationToken;
        var (world, _) = await PrepareAsync(ct);
        var normalizedName = name.Trim().ToUpperInvariant();
        var rowsBefore = await RoleRowsNamedAsync(world, normalizedName, ct);
        using var admin = await world.AsAsync(TestPersona.TenantAdmin, cancellationToken: ct);

        using var response = await admin.PostAsJsonAsync(
            "/api/roles",
            new { name, permissions = new[] { Actions.Access.Hub } },
            ct);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await RoleRowsNamedAsync(world, normalizedName, ct)).Should().Be(rowsBefore);
    }

    [Fact]
    public async Task A_role_manager_cannot_widen_their_own_role()
    {
        var ct = TestContext.Current.CancellationToken;
        var (world, _) = await PrepareAsync(ct);
        using var manager = await ManagerClientAsync(world, ct);

        using var response = await manager.PutAsJsonAsync(
            $"/api/roles/{MANAGER_ROLE}",
            new { permissions = _managerPermissions.Concat(_beyondManager).ToArray() },
            ct);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task A_role_manager_cannot_change_a_role_they_hold_even_within_their_own_permissions()
    {
        var ct = TestContext.Current.CancellationToken;
        var (world, _) = await PrepareAsync(ct);
        using var manager = await ManagerClientAsync(world, ct);

        using var response = await manager.PutAsJsonAsync(
            $"/api/roles/{MANAGER_ROLE}",
            new { permissions = _managerPermissions },
            ct);

        var body = await response.Content.ReadAsStringAsync(ct);
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden, body);
        body.Should().Contain(OWN_ACCESS_MESSAGE);
    }

    [Fact]
    public async Task A_role_manager_cannot_widen_a_role_nobody_holds()
    {
        var ct = TestContext.Current.CancellationToken;
        var (world, _) = await PrepareAsync(ct);
        using var manager = await ManagerClientAsync(world, ct);

        using var response = await manager.PutAsJsonAsync(
            $"/api/roles/{NARROW_ROLE}",
            new { permissions = new[] { Actions.Access.Hub, Actions.Tenant.ManageUsers } },
            ct);

        await ShouldBeRefusedByTheCeilingAsync(response, ct);
    }

    [Fact]
    public async Task A_role_manager_cannot_change_a_role_wider_than_their_own()
    {
        var ct = TestContext.Current.CancellationToken;
        var (world, _) = await PrepareAsync(ct);
        using var manager = await ManagerClientAsync(world, ct);

        using var response = await manager.PutAsJsonAsync(
            $"/api/roles/{CATALOG_ROLE}",
            new { permissions = new[] { Actions.Access.Hub } },
            ct);

        await ShouldBeRefusedByTheCeilingAsync(response, ct);
    }

    [Fact]
    public async Task A_role_manager_can_change_a_role_within_their_own_permissions()
    {
        var ct = TestContext.Current.CancellationToken;
        var (world, _) = await PrepareAsync(ct);
        using var manager = await ManagerClientAsync(world, ct);

        using var response = await manager.PutAsJsonAsync(
            $"/api/roles/{NARROW_ROLE}",
            new { permissions = new[] { Actions.Access.Hub, Actions.Tenant.ManageRoles } },
            ct);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task A_role_manager_cannot_assign_the_built_in_admin_role_to_themselves()
    {
        var ct = TestContext.Current.CancellationToken;
        var (world, managerId) = await PrepareAsync(ct);
        await UseOnlyTheSystemAdminRoleAsync(world, ct);
        using var manager = await ManagerClientAsync(world, ct);

        using var response = await manager.PutAsJsonAsync(
            $"/api/users/{managerId}/roles",
            new { roleNames = new[] { MANAGER_ROLE, SystemRole.Admin.Name } },
            ct);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await HoldsRoleAsync(world, managerId, SystemRole.Admin.Name, ct)).Should().BeFalse();
    }

    [Fact]
    public async Task A_role_manager_cannot_assign_the_built_in_admin_role_to_another_user()
    {
        var ct = TestContext.Current.CancellationToken;
        var (world, _) = await PrepareAsync(ct);
        await UseOnlyTheSystemAdminRoleAsync(world, ct);
        var creator = world.Tenants[0].Creator;
        using var manager = await ManagerClientAsync(world, ct);

        using var response = await manager.PutAsJsonAsync(
            $"/api/users/{creator.Id}/roles",
            new { roleNames = new[] { SystemRole.Creator.Name, SystemRole.Admin.Name } },
            ct);

        await ShouldBeRefusedByTheCeilingAsync(response, ct);
        (await HoldsRoleAsync(world, creator.Id, SystemRole.Admin.Name, ct)).Should().BeFalse();
    }

    [Fact]
    public async Task A_role_manager_cannot_assign_a_wider_role_to_another_user()
    {
        var ct = TestContext.Current.CancellationToken;
        var (world, _) = await PrepareAsync(ct);
        var creator = world.Tenants[0].Creator;
        using var manager = await ManagerClientAsync(world, ct);

        using var response = await manager.PutAsJsonAsync(
            $"/api/users/{creator.Id}/roles",
            new { roleNames = new[] { SystemRole.Creator.Name, CATALOG_ROLE } },
            ct);

        await ShouldBeRefusedByTheCeilingAsync(response, ct);
    }

    [Fact]
    public async Task A_role_manager_cannot_take_a_wider_role_away_from_another_user()
    {
        var ct = TestContext.Current.CancellationToken;
        var (world, _) = await PrepareAsync(ct);
        var creator = world.Tenants[0].Creator;
        await world.Seed.SeedUserRoleAssignmentAsync(world.Tenants[0].Id, creator.UserName, CATALOG_ROLE, ct);
        using var manager = await ManagerClientAsync(world, ct);

        using var response = await manager.PutAsJsonAsync(
            $"/api/users/{creator.Id}/roles",
            new { roleNames = new[] { SystemRole.Creator.Name } },
            ct);

        await ShouldBeRefusedByTheCeilingAsync(response, ct);
        (await HoldsRoleAsync(world, creator.Id, CATALOG_ROLE, ct)).Should().BeTrue();
    }

    [Fact]
    public async Task A_role_manager_can_assign_a_role_within_their_own_permissions_to_another_user()
    {
        var ct = TestContext.Current.CancellationToken;
        var (world, _) = await PrepareAsync(ct);
        var tenantId = world.Tenants[0].Id;
        const string otherUser = "plain-user";
        await world.Seed.SeedUserAsync(tenantId, otherUser, $"{otherUser}@test.local", password: SEED_PASSWORD, cancellationToken: ct);
        var otherUserId = await UserIdAsync(world, otherUser, ct);
        using var manager = await ManagerClientAsync(world, ct);

        using var response = await manager.PutAsJsonAsync(
            $"/api/users/{otherUserId}/roles",
            new { roleNames = new[] { NARROW_ROLE } },
            ct);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await HoldsRoleAsync(world, otherUserId, NARROW_ROLE, ct)).Should().BeTrue();
    }

    [Fact]
    public async Task A_role_manager_cannot_change_their_own_roles()
    {
        var ct = TestContext.Current.CancellationToken;
        var (world, managerId) = await PrepareAsync(ct);
        using var manager = await ManagerClientAsync(world, ct);

        using var response = await manager.PutAsJsonAsync(
            $"/api/users/{managerId}/roles",
            new { roleNames = new[] { MANAGER_ROLE, NARROW_ROLE } },
            ct);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await HoldsRoleAsync(world, managerId, NARROW_ROLE, ct)).Should().BeFalse();
    }

    [Fact]
    public async Task A_role_manager_cannot_assign_a_role_they_created_to_themselves()
    {
        var ct = TestContext.Current.CancellationToken;
        var (world, managerId) = await PrepareAsync(ct);
        using var manager = await ManagerClientAsync(world, ct);
        using var created = await manager.PostAsJsonAsync(
            "/api/roles",
            new { name = "Wider", permissions = _beyondManager },
            ct);

        using var response = await manager.PutAsJsonAsync(
            $"/api/users/{managerId}/roles",
            new { roleNames = new[] { MANAGER_ROLE, "Wider" } },
            ct);

        response.StatusCode.Should().NotBe(HttpStatusCode.OK,
            $"the role create answered {(int)created.StatusCode} and must not become self-assignable");
    }

    [Fact]
    public async Task A_role_manager_still_cannot_list_users_after_asking_for_admin()
    {
        var ct = TestContext.Current.CancellationToken;
        var (world, managerId) = await PrepareAsync(ct);
        await UseOnlyTheSystemAdminRoleAsync(world, ct);
        using (var before = await ManagerClientAsync(world, ct))
        {
            using var denied = await before.GetAsync("/api/users", ct);
            denied.StatusCode.Should().Be(HttpStatusCode.Forbidden);
            using var _ = await before.PutAsJsonAsync(
                $"/api/users/{managerId}/roles",
                new { roleNames = new[] { MANAGER_ROLE, SystemRole.Admin.Name } },
                ct);
        }

        using var fresh = await ManagerClientAsync(world, ct);
        using var users = await fresh.GetAsync("/api/users", ct);

        users.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task A_role_manager_cannot_take_admin_away_from_a_tenant_admin()
    {
        var ct = TestContext.Current.CancellationToken;
        var (world, _) = await PrepareAsync(ct);
        var admin = world.Tenants[0].Admin;
        using var manager = await ManagerClientAsync(world, ct);

        using var response = await manager.PutAsJsonAsync(
            $"/api/users/{admin.Id}/roles",
            new { roleNames = new[] { SystemRole.Creator.Name } },
            ct);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await HoldsRoleAsync(world, admin.Id, SystemRole.Admin.Name, ct)).Should().BeTrue();
    }

    [Fact]
    public async Task A_tenant_admin_can_assign_the_built_in_admin_role()
    {
        var ct = TestContext.Current.CancellationToken;
        var (world, _) = await PrepareAsync(ct);
        var creator = world.Tenants[0].Creator;
        using var admin = await world.AsAsync(TestPersona.TenantAdmin, cancellationToken: ct);

        using var response = await admin.PutAsJsonAsync(
            $"/api/users/{creator.Id}/roles",
            new { roleNames = new[] { SystemRole.Creator.Name, SystemRole.Admin.Name } },
            ct);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await HoldsRoleAsync(world, creator.Id, SystemRole.Admin.Name, ct)).Should().BeTrue();
    }

    [Fact]
    public async Task An_inviter_cannot_invite_with_a_role_wider_than_their_own()
    {
        var ct = TestContext.Current.CancellationToken;
        var (world, _) = await PrepareAsync(ct);
        using var inviter = await InviterClientAsync(world, ct);
        var email = $"invitee-{Guid.NewGuid():N}@endatix.com";

        using var response = await inviter.PostAsJsonAsync(
            "/api/users",
            new { email, roles = new[] { MANAGER_ROLE } },
            ct);

        await ShouldBeRefusedByTheCeilingAsync(response, ct);
        (await UserExistsAsync(world, email, ct)).Should().BeFalse("a refused invite must not create the account");
    }

    [Fact]
    public async Task An_inviter_can_invite_with_a_role_within_their_own_permissions()
    {
        var ct = TestContext.Current.CancellationToken;
        var (world, _) = await PrepareAsync(ct);
        using var inviter = await InviterClientAsync(world, ct);
        var email = $"invitee-{Guid.NewGuid():N}@endatix.com";

        using var response = await inviter.PostAsJsonAsync(
            "/api/users",
            new { email, roles = new[] { NARROW_ROLE } },
            ct);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task A_user_manager_cannot_remove_a_tenant_admins_access()
    {
        var ct = TestContext.Current.CancellationToken;
        var (world, _) = await PrepareAsync(ct);
        var admin = world.Tenants[0].Admin;
        const string secondAdmin = "second-admin";
        await world.Seed.SeedUserAsync(world.Tenants[0].Id, secondAdmin, $"{secondAdmin}@test.local", password: SEED_PASSWORD, cancellationToken: ct);
        await world.Seed.SeedUserRoleAssignmentAsync(world.Tenants[0].Id, secondAdmin, SystemRole.Admin.Name, ct);
        using var userManager = await UserManagerClientAsync(world, ct);

        using var response = await userManager.DeleteAsync($"/api/users/{admin.Id}", ct);

        await ShouldBeRefusedByTheCeilingAsync(response, ct);
        (await HoldsRoleAsync(world, admin.Id, SystemRole.Admin.Name, ct)).Should().BeTrue();
    }

    [Fact]
    public async Task A_user_manager_cannot_lock_out_a_tenant_admin()
    {
        var ct = TestContext.Current.CancellationToken;
        var (world, _) = await PrepareAsync(ct);
        var admin = world.Tenants[0].Admin;
        using var userManager = await UserManagerClientAsync(world, ct);

        using var response = await userManager.PostAsync($"/api/users/{admin.Id}/lockout", null, ct);

        await ShouldBeRefusedByTheCeilingAsync(response, ct);
    }

    [Fact]
    public async Task A_user_manager_cannot_unlock_a_tenant_admin()
    {
        var ct = TestContext.Current.CancellationToken;
        var (world, _) = await PrepareAsync(ct);
        var admin = world.Tenants[0].Admin;
        await LockOutAsync(world, admin.Id, ct);
        using var userManager = await UserManagerClientAsync(world, ct);

        using var response = await userManager.PostAsync($"/api/users/{admin.Id}/unlock", null, ct);

        await ShouldBeRefusedByTheCeilingAsync(response, ct);
    }

    [Fact]
    public async Task A_user_manager_cannot_lock_out_a_user_whose_identity_provider_makes_them_admin()
    {
        var ct = TestContext.Current.CancellationToken;
        var (world, _) = await PrepareAsync(ct);
        var tenantId = world.Tenants[0].Id;
        const string externalUser = "idp-admin";
        await world.Seed.SeedUserAsync(tenantId, externalUser, $"{externalUser}@test.local", cancellationToken: ct);
        var externalUserId = await UserIdAsync(world, externalUser, ct);
        await MakeExternalAsync(world, externalUserId, [SystemRole.Admin.Name], ct);
        using var userManager = await UserManagerClientAsync(world, ct);

        using var response = await userManager.PostAsync($"/api/users/{externalUserId}/lockout", null, ct);

        await ShouldBeRefusedByTheCeilingAsync(response, ct);
    }

    [Fact]
    public async Task A_tenant_role_named_admin_does_not_count_as_another_admin()
    {
        var ct = TestContext.Current.CancellationToken;
        var (world, _) = await PrepareAsync(ct);
        var tenantId = world.Tenants[0].Id;
        var admin = world.Tenants[0].Admin;
        const string lookalikeUser = "lookalike-admin";
        await AddLookalikeRoleAsync(world, tenantId, SystemRole.Admin.Name, ct);
        await world.Seed.SeedUserAsync(tenantId, lookalikeUser, $"{lookalikeUser}@test.local", password: SEED_PASSWORD, cancellationToken: ct);
        await AssignByIdAsync(world, await UserIdAsync(world, lookalikeUser, ct), await LookalikeRoleIdAsync(world, tenantId, SystemRole.Admin.Name, ct), ct);
        using var platformAdmin = await world.AsAsync(TestPersona.PlatformAdmin, cancellationToken: ct);

        using var response = await platformAdmin.DeleteAsync($"/api/users/{admin.Id}", ct);

        response.StatusCode.Should().Be(HttpStatusCode.Conflict, await response.Content.ReadAsStringAsync(ct));
        (await HoldsRoleAsync(world, admin.Id, SystemRole.Admin.Name, ct)).Should().BeTrue();
    }

    [Fact]
    public async Task A_user_manager_can_remove_access_of_a_user_within_their_own_permissions()
    {
        var ct = TestContext.Current.CancellationToken;
        var (world, _) = await PrepareAsync(ct);
        var tenantId = world.Tenants[0].Id;
        const string otherUser = "narrow-user";
        await world.Seed.SeedUserAsync(tenantId, otherUser, $"{otherUser}@test.local", password: SEED_PASSWORD, cancellationToken: ct);
        await world.Seed.SeedUserRoleAssignmentAsync(tenantId, otherUser, NARROW_ROLE, ct);
        var otherUserId = await UserIdAsync(world, otherUser, ct);
        using var userManager = await UserManagerClientAsync(world, ct);

        using var locked = await userManager.PostAsync($"/api/users/{otherUserId}/lockout", null, ct);
        using var removed = await userManager.DeleteAsync($"/api/users/{otherUserId}", ct);

        locked.StatusCode.Should().Be(HttpStatusCode.OK);
        removed.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task An_inviter_cannot_cancel_an_invite_that_carries_a_wider_role()
    {
        var ct = TestContext.Current.CancellationToken;
        var (world, _) = await PrepareAsync(ct);
        var tenantId = world.Tenants[0].Id;
        const string pendingUser = "pending-invitee";
        await world.Seed.SeedUserAsync(tenantId, pendingUser, $"{pendingUser}@test.local", cancellationToken: ct);
        await world.Seed.SeedUserRoleAssignmentAsync(tenantId, pendingUser, CATALOG_ROLE, ct);
        var pendingUserId = await UserIdAsync(world, pendingUser, ct);
        await MarkUnverifiedAsync(world, pendingUserId, ct);
        using var inviter = await InviterClientAsync(world, ct);

        using var response = await inviter.DeleteAsync($"/api/users/{pendingUserId}/invite", ct);

        await ShouldBeRefusedByTheCeilingAsync(response, ct);
        (await HoldsRoleAsync(world, pendingUserId, CATALOG_ROLE, ct)).Should().BeTrue();
    }

    [Fact]
    public async Task A_role_assignment_without_a_signed_in_user_is_refused()
    {
        var ct = TestContext.Current.CancellationToken;
        var (world, _) = await PrepareAsync(ct);
        var creator = world.Tenants[0].Creator;
        using var scope = world.Services.CreateScope();
        var roles = scope.ServiceProvider.GetRequiredService<IRoleManagementService>();

        var result = await roles.AssignRoleToUserAsync(creator.Id, CATALOG_ROLE, ct);

        result.Status.Should().Be(ResultStatus.Forbidden);
        (await HoldsRoleAsync(world, creator.Id, CATALOG_ROLE, ct)).Should().BeFalse();
    }

    [Fact]
    public async Task A_tenant_role_named_admin_does_not_make_its_holder_an_admin()
    {
        var ct = TestContext.Current.CancellationToken;
        var (world, _) = await PrepareAsync(ct);
        var tenantId = world.Tenants[0].Id;
        const string lookalikeUser = "lookalike";
        await AddLookalikeRoleAsync(world, tenantId, SystemRole.Admin.Name, ct);
        await world.Seed.SeedUserAsync(tenantId, lookalikeUser, $"{lookalikeUser}@test.local", password: SEED_PASSWORD, cancellationToken: ct);
        await AssignByIdAsync(world, await UserIdAsync(world, lookalikeUser, ct), await LookalikeRoleIdAsync(world, tenantId, SystemRole.Admin.Name, ct), ct);
        using var lookalike = await world.AsAsync(TestPersona.CustomRole(SystemRole.Admin.Name), userName: lookalikeUser, cancellationToken: ct);

        using var users = await lookalike.GetAsync("/api/users", ct);

        users.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task A_tenant_role_named_platform_admin_does_not_open_platform_endpoints()
    {
        var ct = TestContext.Current.CancellationToken;
        var (world, _) = await PrepareAsync(ct);
        var tenantId = world.Tenants[0].Id;
        const string lookalikeUser = "platform-lookalike";
        await AddLookalikeRoleAsync(world, tenantId, SystemRole.PlatformAdmin.Name, ct);
        await world.Seed.SeedUserAsync(tenantId, lookalikeUser, $"{lookalikeUser}@test.local", password: SEED_PASSWORD, cancellationToken: ct);
        await AssignByIdAsync(world, await UserIdAsync(world, lookalikeUser, ct), await LookalikeRoleIdAsync(world, tenantId, SystemRole.PlatformAdmin.Name, ct), ct);
        using var lookalike = await world.AsAsync(TestPersona.CustomRole(SystemRole.PlatformAdmin.Name), userName: lookalikeUser, cancellationToken: ct);

        using var tenants = await lookalike.GetAsync("/api/admin/tenants", ct);

        tenants.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task A_tenant_role_holding_a_platform_permission_grants_none_of_it()
    {
        var ct = TestContext.Current.CancellationToken;
        var (world, _) = await PrepareAsync(ct);
        var tenantId = world.Tenants[0].Id;
        var creator = world.Tenants[0].Creator;
        await world.Seed.SeedRolePermissionsAsync(tenantId, CATALOG_ROLE, [Actions.Platform.ManageTenants], ct);
        await world.Seed.SeedUserRoleAssignmentAsync(tenantId, creator.UserName, CATALOG_ROLE, ct);

        var access = await AuthorizationDataAsync(world, creator.Id, tenantId, ct);

        access.Permissions.Should().Contain(Actions.Tenant.ManageUsers);
        access.Permissions.Should().NotContain(permission => permission.StartsWith("platform."));
    }

    [Fact]
    public async Task The_built_in_platform_admin_role_still_grants_its_platform_permissions()
    {
        var ct = TestContext.Current.CancellationToken;
        var (world, _) = await PrepareAsync(ct);
        var tenantId = world.Tenants[0].Id;
        var platformAdmin = world.Tenants[0].PlatformAdmin;
        await world.Seed.SeedRolePermissionsAsync(0, SystemRole.PlatformAdmin.Name, [Actions.Platform.ManageTenants], ct);

        var access = await AuthorizationDataAsync(world, platformAdmin.Id, tenantId, ct);

        access.Permissions.Should().Contain(Actions.Platform.ManageTenants);
    }

    [Fact]
    public async Task A_system_assignment_attaches_the_role_of_the_users_own_tenant_when_two_tenants_share_its_name()
    {
        var ct = TestContext.Current.CancellationToken;
        var world = await PrepareSharedRoleNameAsync(ct);
        var (tenantA, tenantB) = (world.Tenants[0], world.Tenants[1]);
        using var scope = world.Services.CreateScope();
        var systemRoles = scope.ServiceProvider.GetRequiredService<ISystemRoleAssignment>();

        var result = await systemRoles.AssignRoleAsSystemAsync(tenantA.Creator.Id, $" {SHARED_ROLE} ", ct);
        var again = await systemRoles.AssignRoleAsSystemAsync(tenantA.Creator.Id, SHARED_ROLE, ct);

        result.IsSuccess.Should().BeTrue(string.Join(", ", result.Errors));
        again.IsInvalid().Should().BeTrue();
        (await HoldsTenantRoleAsync(world, tenantA.Creator.Id, tenantA.Id, SHARED_ROLE, ct)).Should().BeTrue();
        (await HoldsTenantRoleAsync(world, tenantA.Creator.Id, tenantB.Id, SHARED_ROLE, ct)).Should().BeFalse();
    }

    [Fact]
    public async Task A_system_assignment_grants_the_built_in_platform_admin_role()
    {
        var ct = TestContext.Current.CancellationToken;
        var world = await PrepareSharedRoleNameAsync(ct);
        var creator = world.Tenants[0].Creator;
        using var scope = world.Services.CreateScope();
        var systemRoles = scope.ServiceProvider.GetRequiredService<ISystemRoleAssignment>();

        var result = await systemRoles.AssignRoleAsSystemAsync(creator.Id, SystemRole.PlatformAdmin.Name, ct);

        result.IsSuccess.Should().BeTrue(string.Join(", ", result.Errors));
        (await HoldsTenantRoleAsync(world, creator.Id, 0, SystemRole.PlatformAdmin.Name, ct)).Should().BeTrue();
    }

    /// <remarks>
    /// The invite rollback removes roles through this same call, in the inviter's request; no HTTP request reaches
    /// the rollback, because the invite checks every role before it creates the account.
    /// </remarks>
    [Fact]
    public async Task Removing_a_role_takes_the_role_of_the_callers_tenant_when_two_tenants_share_its_name()
    {
        var ct = TestContext.Current.CancellationToken;
        var world = await PrepareSharedRoleNameAsync(ct);
        var (tenantA, tenantB) = (world.Tenants[0], world.Tenants[1]);
        await world.Seed.SeedUserRoleAssignmentAsync(tenantA.Id, tenantA.Creator.UserName, SHARED_ROLE, ct);
        await world.Seed.SeedUserRoleAssignmentAsync(tenantB.Id, tenantB.Creator.UserName, SHARED_ROLE, ct);
        using var scope = SignedInScope(world, tenantA.Admin, [SystemRole.Admin.Name]);
        var roles = scope.ServiceProvider.GetRequiredService<IRoleManagementService>();

        Result result;
        Result again;
        try
        {
            result = await roles.RemoveRoleFromUserAsync(tenantA.Creator.Id, SHARED_ROLE, ct);
            again = await roles.RemoveRoleFromUserAsync(tenantA.Creator.Id, SHARED_ROLE, ct);
        }
        finally
        {
            scope.ServiceProvider.GetRequiredService<IHttpContextAccessor>().HttpContext = null;
        }

        result.IsSuccess.Should().BeTrue(string.Join(", ", result.Errors));
        again.IsInvalid().Should().BeTrue();
        (await HoldsTenantRoleAsync(world, tenantA.Creator.Id, tenantA.Id, SHARED_ROLE, ct)).Should().BeFalse();
        (await HoldsTenantRoleAsync(world, tenantB.Creator.Id, tenantB.Id, SHARED_ROLE, ct)).Should().BeTrue();
    }

    private async Task<(IntegrationTestWorld World, long ManagerId)> PrepareAsync(CancellationToken ct)
    {
        var world = await fixture.PrepareWorldAsync(
            IntegrationWorldOptions.SingleTenant with { DefaultPassword = SEED_PASSWORD }, ct);
        var tenantId = world.Tenants[0].Id;

        // The permissions the manager lacks must exist in the catalog, so a refusal is about the ceiling and
        // not about an unknown permission name. A role nobody holds carries them.
        await world.Seed.SeedRoleAsync(tenantId, CATALOG_ROLE, ct);
        await world.Seed.SeedRolePermissionsAsync(tenantId, CATALOG_ROLE, _beyondManager, ct);

        await world.Seed.SeedRoleAsync(tenantId, NARROW_ROLE, ct);
        await world.Seed.SeedRolePermissionsAsync(tenantId, NARROW_ROLE, [Actions.Access.Hub], ct);

        await world.Seed.SeedRoleAsync(tenantId, MANAGER_ROLE, ct);
        await world.Seed.SeedRolePermissionsAsync(tenantId, MANAGER_ROLE, _managerPermissions, ct);
        await world.Seed.SeedUserAsync(tenantId, MANAGER_USER, $"{MANAGER_USER}@test.local", password: SEED_PASSWORD, cancellationToken: ct);
        await world.Seed.SeedUserRoleAssignmentAsync(tenantId, MANAGER_USER, MANAGER_ROLE, ct);

        return (world, await UserIdAsync(world, MANAGER_USER, ct));
    }

    /// <summary>Two tenants, each with its own custom role of the same name.</summary>
    private async Task<IntegrationTestWorld> PrepareSharedRoleNameAsync(CancellationToken ct)
    {
        var world = await fixture.PrepareWorldAsync(IntegrationWorldOptions.MultiTenant, ct);
        world.Tenants.Should().HaveCountGreaterThanOrEqualTo(2);
        foreach (var tenant in world.Tenants.Take(2))
        {
            await world.Seed.SeedRoleAsync(tenant.Id, SHARED_ROLE, ct);
            await world.Seed.SeedRolePermissionsAsync(tenant.Id, SHARED_ROLE, [Actions.Access.Hub], ct);
        }

        return world;
    }

    /// <summary>A service scope that runs as the signed-in user in the user's tenant, as an API request does.</summary>
    private static IServiceScope SignedInScope(IntegrationTestWorld world, SeededUser user, string[] roles)
    {
        var scope = world.Services.CreateScope();
        var access = AuthorizationData.ForAuthenticatedUser(user.Id.ToString(), user.TenantId, roles, []);
        var principal = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, user.Id.ToString())], "test"));
        principal.AddIdentity(new AuthorizedIdentity(access));
        scope.ServiceProvider.GetRequiredService<IHttpContextAccessor>().HttpContext =
            new DefaultHttpContext { User = principal, RequestServices = scope.ServiceProvider };
        ((TenantContext)scope.ServiceProvider.GetRequiredService<ITenantContext>()).SetTenant(user.TenantId);
        return scope;
    }

    private static Task<HttpClient> ManagerClientAsync(IntegrationTestWorld world, CancellationToken ct) =>
        world.AsAsync(TestPersona.CustomRole(MANAGER_ROLE), userName: MANAGER_USER, cancellationToken: ct);

    internal static async Task<HttpClient> UserManagerClientAsync(IntegrationTestWorld world, CancellationToken ct)
    {
        var tenantId = world.Tenants[0].Id;
        const string userManagerRole = "UserManagerOnly";
        const string userManagerUser = "user-manager";
        await world.Seed.SeedRoleAsync(tenantId, userManagerRole, ct);
        await world.Seed.SeedRolePermissionsAsync(
            tenantId, userManagerRole, [Actions.Access.Hub, Actions.Tenant.ViewUsers, Actions.Tenant.ManageUsers], ct);
        await world.Seed.SeedUserAsync(tenantId, userManagerUser, $"{userManagerUser}@test.local", password: SEED_PASSWORD, cancellationToken: ct);
        await world.Seed.SeedUserRoleAssignmentAsync(tenantId, userManagerUser, userManagerRole, ct);
        return await world.AsAsync(TestPersona.CustomRole(userManagerRole), userName: userManagerUser, cancellationToken: ct);
    }

    private static async Task<HttpClient> InviterClientAsync(IntegrationTestWorld world, CancellationToken ct)
    {
        var tenantId = world.Tenants[0].Id;
        const string inviterRole = "InviterOnly";
        const string inviterUser = "inviter";
        await world.Seed.SeedRoleAsync(tenantId, inviterRole, ct);
        await world.Seed.SeedRolePermissionsAsync(tenantId, inviterRole, [Actions.Access.Hub, Actions.Tenant.InviteUsers], ct);
        await world.Seed.SeedUserAsync(tenantId, inviterUser, $"{inviterUser}@test.local", password: SEED_PASSWORD, cancellationToken: ct);
        await world.Seed.SeedUserRoleAssignmentAsync(tenantId, inviterUser, inviterRole, ct);
        return await world.AsAsync(TestPersona.CustomRole(inviterRole), userName: inviterUser, cancellationToken: ct);
    }

    private static async Task ShouldBeRefusedByTheCeilingAsync(HttpResponseMessage response, CancellationToken ct)
    {
        var body = await response.Content.ReadAsStringAsync(ct);
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden, body);
        body.Should().Contain(CEILING_MESSAGE);
    }

    private static async Task<AuthorizationData> AuthorizationDataAsync(
        IntegrationTestWorld world, long userId, long tenantId, CancellationToken ct)
    {
        using var scope = world.Services.CreateScope();
        var provider = scope.ServiceProvider.GetRequiredService<IAuthorizationDataProvider>();
        var result = await provider.GetAuthorizationDataAsync(userId, tenantId, ct);
        result.IsSuccess.Should().BeTrue(string.Join(", ", result.Errors));
        return result.Value;
    }

    private static async Task<long> UserIdAsync(IntegrationTestWorld world, string userName, CancellationToken ct)
    {
        using var scope = world.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppIdentityDbContext>();
        var tenantId = world.Tenants[0].Id;
        return await db.Users
            .Where(user => user.TenantId == tenantId && user.UserName == userName)
            .Select(user => user.Id)
            .SingleAsync(ct);
    }

    private static async Task LockOutAsync(IntegrationTestWorld world, long userId, CancellationToken ct)
    {
        using var scope = world.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppIdentityDbContext>();
        var user = await db.Users.SingleAsync(item => item.Id == userId, ct);
        user.LockoutEnabled = true;
        user.LockoutEnd = DateTimeOffset.MaxValue;
        await db.SaveChangesAsync(ct);
    }

    private static async Task MakeExternalAsync(IntegrationTestWorld world, long userId, string[] externalRoles, CancellationToken ct)
    {
        using var scope = world.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppIdentityDbContext>();
        var user = await db.Users.SingleAsync(item => item.Id == userId, ct);
        user.AuthProvider = "keycloak";
        user.ExternalRolesJson = System.Text.Json.JsonSerializer.Serialize(externalRoles);
        await db.SaveChangesAsync(ct);
    }

    private static async Task MarkUnverifiedAsync(IntegrationTestWorld world, long userId, CancellationToken ct)
    {
        using var scope = world.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppIdentityDbContext>();
        var user = await db.Users.SingleAsync(item => item.Id == userId, ct);
        user.EmailConfirmed = false;
        await db.SaveChangesAsync(ct);
    }

    private static async Task<bool> UserExistsAsync(IntegrationTestWorld world, string email, CancellationToken ct)
    {
        using var scope = world.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppIdentityDbContext>();
        var normalizedEmail = email.ToUpperInvariant();
        return await db.Users.AnyAsync(user => user.NormalizedEmail == normalizedEmail, ct);
    }

    private static async Task<bool> HoldsRoleAsync(IntegrationTestWorld world, long userId, string roleName, CancellationToken ct)
    {
        using var scope = world.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppIdentityDbContext>();
        return await db.UserRoles
            .Join(db.Roles, userRole => userRole.RoleId, role => role.Id, (userRole, role) => new { userRole.UserId, role.Name })
            .AnyAsync(item => item.UserId == userId && item.Name == roleName, ct);
    }

    private static async Task<int> RoleRowsNamedAsync(IntegrationTestWorld world, string normalizedName, CancellationToken ct)
    {
        using var scope = world.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppIdentityDbContext>();
        return await db.Roles.CountAsync(role => role.NormalizedName == normalizedName, ct);
    }

    private static async Task<bool> HoldsTenantRoleAsync(
        IntegrationTestWorld world, long userId, long roleTenantId, string roleName, CancellationToken ct)
    {
        using var scope = world.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppIdentityDbContext>();
        return await db.UserRoles
            .Join(db.Roles, userRole => userRole.RoleId, role => role.Id, (userRole, role) => new { userRole.UserId, role.Name, role.TenantId })
            .AnyAsync(item => item.UserId == userId && item.Name == roleName && item.TenantId == roleTenantId, ct);
    }

    /// <summary>
    /// A tenant role that only borrows a system role's name, as a future role writer (an import, a sync) could create.
    /// It carries the hub permission only.
    /// </summary>
    private static async Task AddLookalikeRoleAsync(IntegrationTestWorld world, long tenantId, string name, CancellationToken ct)
    {
        using var scope = world.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppIdentityDbContext>();
        var idGenerator = scope.ServiceProvider.GetRequiredService<IIdGenerator<long>>();
        var normalizedName = name.ToUpperInvariant();
        var copies = await db.Roles.Where(role => role.NormalizedName == normalizedName && role.TenantId == tenantId).ToListAsync(ct);
        if (copies.Count == 0)
        {
            db.Roles.Add(new AppRole
            {
                Id = idGenerator.CreateId(),
                Name = name,
                NormalizedName = normalizedName,
                TenantId = tenantId,
                IsSystemDefined = false,
                IsActive = true
            });
        }
        else
        {
            copies.ForEach(role => role.IsSystemDefined = false);
        }

        await db.SaveChangesAsync(ct);
        await world.Seed.SeedRolePermissionsAsync(tenantId, name, [Actions.Access.Hub], ct);
    }

    private static async Task<long> LookalikeRoleIdAsync(IntegrationTestWorld world, long tenantId, string name, CancellationToken ct)
    {
        using var scope = world.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppIdentityDbContext>();
        var normalizedName = name.ToUpperInvariant();
        return await db.Roles
            .Where(role => role.NormalizedName == normalizedName && role.TenantId == tenantId)
            .Select(role => role.Id)
            .SingleAsync(ct);
    }

    private static async Task AssignByIdAsync(IntegrationTestWorld world, long userId, long roleId, CancellationToken ct)
    {
        using var scope = world.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppIdentityDbContext>();
        db.UserRoles.Add(new IdentityUserRole<long> { UserId = userId, RoleId = roleId });
        await db.SaveChangesAsync(ct);
    }

    /// <summary>
    /// Production has one Admin row: the system role at tenant 0 from the RolesAndPermissions migration. Make sure
    /// the world has exactly that shape, so the test reaches the system-role branch.
    /// </summary>
    private static async Task UseOnlyTheSystemAdminRoleAsync(IntegrationTestWorld world, CancellationToken ct)
    {
        using var scope = world.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppIdentityDbContext>();
        var idGenerator = scope.ServiceProvider.GetRequiredService<IIdGenerator<long>>();
        var normalizedAdmin = SystemRole.Admin.Name.ToUpperInvariant();

        var copies = await db.Roles
            .Where(role => role.NormalizedName == normalizedAdmin && !(role.TenantId <= 0 && role.IsSystemDefined))
            .ToListAsync(ct);
        var copyIds = copies.Select(role => role.Id).ToList();
        db.UserRoles.RemoveRange(await db.UserRoles.Where(userRole => copyIds.Contains(userRole.RoleId)).ToListAsync(ct));
        db.RolePermissions.RemoveRange(await db.RolePermissions.Where(rp => copyIds.Contains(rp.RoleId)).ToListAsync(ct));
        db.Roles.RemoveRange(copies);

        if (!await db.Roles.AnyAsync(role => role.NormalizedName == normalizedAdmin && role.TenantId <= 0 && role.IsSystemDefined, ct))
        {
            var systemAdmin = AppRole.CreateSystemRole(SystemRole.Admin.Name, SystemRole.Admin.Description, tenantId: 0);
            systemAdmin.Id = idGenerator.CreateId();
            systemAdmin.NormalizedName = normalizedAdmin;
            db.Roles.Add(systemAdmin);
        }

        await db.SaveChangesAsync(ct);

        var admins = await db.Roles.Where(role => role.NormalizedName == normalizedAdmin).ToListAsync(ct);
        admins.Should().ContainSingle().Which.Should().Match<AppRole>(role => role.TenantId == 0 && role.IsSystemDefined);
    }
}
