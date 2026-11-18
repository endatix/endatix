using Endatix.Core.Abstractions.Authorization;
using Endatix.Core.Entities.Identity;
using Endatix.Infrastructure.Identity;
using Endatix.Infrastructure.Identity.Authentication;
using Endatix.Infrastructure.Identity.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Endatix.Infrastructure.Tests.Identity.Authorization;

public sealed class PlatformAdminLocalApprovalGateTests
{
    [Fact]
    public async Task ApplyAsync_NoPlatformAdminRole_ReturnsMappedAuthorizationUnchanged()
    {
        // Arrange
        var dbOptions = new DbContextOptionsBuilder<AppIdentityDbContext>().Options;
        var db = Substitute.For<AppIdentityDbContext>(dbOptions);
        var gate = new PlatformAdminLocalApprovalGate(db, new UpperInvariantLookupNormalizer());

        // Act
        var result = await gate.ApplyAsync(
            tenantId: 1,
            authProvider: AuthProviders.Keycloak,
            externalSubjectId: "external-1",
            userId: null,
            mappedRoles: [SystemRole.Admin.Name],
            mappedPermissions: [Actions.Access.Hub],
            CancellationToken.None);

        // Assert
        result.Roles.Should().BeEquivalentTo([SystemRole.Admin.Name]);
        result.Permissions.Should().BeEquivalentTo([Actions.Access.Hub]);
    }

    [Fact]
    public async Task ApplyAsync_WithoutApproval_DropsPlatformPermissionsOfTenantRoles()
    {
        // Arrange
        await using var db = CreateDatabase();
        var operators = AddRole(db, "Operators", TENANT_ID, isSystemDefined: false, Actions.Access.Hub, Actions.Platform.ManageTenants);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        var gate = new PlatformAdminLocalApprovalGate(db, new UpperInvariantLookupNormalizer());

        // Act
        var result = await ApplyWithoutApprovalAsync(gate, operators.Name!);

        // Assert
        result.Roles.Should().BeEquivalentTo([operators.Name]);
        result.Permissions.Should().BeEquivalentTo([Actions.Access.Hub]);
    }

    [Fact]
    public async Task ApplyAsync_WithoutApproval_IgnoresAnotherTenantsRoleOfTheSameName()
    {
        // Arrange
        await using var db = CreateDatabase();
        AddRole(db, "Operators", TENANT_ID, isSystemDefined: false, Actions.Access.Hub);
        AddRole(db, "Operators", OTHER_TENANT_ID, isSystemDefined: false, Actions.Tenant.ManageUsers);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        var gate = new PlatformAdminLocalApprovalGate(db, new UpperInvariantLookupNormalizer());

        // Act
        var result = await ApplyWithoutApprovalAsync(gate, "Operators");

        // Assert
        result.Permissions.Should().BeEquivalentTo([Actions.Access.Hub]);
    }

    [Fact]
    public async Task ApplyAsync_TenantRoleNamedPlatformAdmin_IsNotLocalApproval()
    {
        // Arrange
        await using var db = CreateDatabase();
        var lookalike = AddRole(db, SystemRole.PlatformAdmin.Name, TENANT_ID, isSystemDefined: false, Actions.Access.Hub);
        db.UserRoles.Add(new IdentityUserRole<long> { UserId = USER_ID, RoleId = lookalike.Id });
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        var gate = new PlatformAdminLocalApprovalGate(db, new UpperInvariantLookupNormalizer());

        // Act
        var result = await gate.ApplyAsync(
            TENANT_ID,
            AuthProviders.Keycloak,
            "external-1",
            USER_ID,
            [SystemRole.PlatformAdmin.Name],
            [Actions.Platform.ManageTenants],
            TestContext.Current.CancellationToken);

        // Assert
        result.Roles.Should().BeEmpty();
        result.Permissions.Should().BeEmpty();
    }

    [Fact]
    public async Task ApplyAsync_UserAssignedTheBuiltInPlatformAdminRow_KeepsRoleAndPermissions()
    {
        // Arrange
        await using var db = CreateDatabase();
        var platformAdmin = AddRole(db, SystemRole.PlatformAdmin.Name, 0, isSystemDefined: true, Actions.Platform.ManageTenants);
        db.UserRoles.Add(new IdentityUserRole<long> { UserId = USER_ID, RoleId = platformAdmin.Id });
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        var gate = new PlatformAdminLocalApprovalGate(db, new UpperInvariantLookupNormalizer());

        // Act
        var result = await gate.ApplyAsync(
            TENANT_ID,
            AuthProviders.Keycloak,
            "external-1",
            USER_ID,
            [SystemRole.PlatformAdmin.Name],
            [Actions.Platform.ManageTenants],
            TestContext.Current.CancellationToken);

        // Assert
        result.Roles.Should().BeEquivalentTo([SystemRole.PlatformAdmin.Name]);
        result.Permissions.Should().BeEquivalentTo([Actions.Platform.ManageTenants]);
    }

    [Fact]
    public async Task ApplyAsync_SubjectAssignedTheBuiltInPlatformAdminRow_KeepsRoleAndPermissions()
    {
        // Arrange
        await using var db = CreateDatabase();
        var platformAdmin = AddRole(db, SystemRole.PlatformAdmin.Name, 0, isSystemDefined: true, Actions.Platform.ManageTenants);
        db.Users.Add(new AppUser
        {
            Id = USER_ID,
            TenantId = TENANT_ID,
            UserName = "external-admin@example.com",
            AuthProvider = AuthProviders.Keycloak,
            ExternalSubjectId = "external-1"
        });
        db.UserRoles.Add(new IdentityUserRole<long> { UserId = USER_ID, RoleId = platformAdmin.Id });
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        var gate = new PlatformAdminLocalApprovalGate(db, new UpperInvariantLookupNormalizer());

        // Act
        var result = await gate.ApplyAsync(
            TENANT_ID,
            AuthProviders.Keycloak,
            "external-1",
            userId: null,
            [SystemRole.PlatformAdmin.Name],
            [Actions.Platform.ManageTenants],
            TestContext.Current.CancellationToken);

        // Assert
        result.Roles.Should().BeEquivalentTo([SystemRole.PlatformAdmin.Name]);
        result.Permissions.Should().BeEquivalentTo([Actions.Platform.ManageTenants]);
    }

    private const long TENANT_ID = 1;
    private const long OTHER_TENANT_ID = 2;
    private const long USER_ID = 500;
    private long _nextId = 1000;

    private static AppIdentityDbContext CreateDatabase() =>
        new(new DbContextOptionsBuilder<AppIdentityDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .ConfigureWarnings(warnings => warnings.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options);

    private static Task<PlatformAdminFilteredAuthorization> ApplyWithoutApprovalAsync(PlatformAdminLocalApprovalGate gate, string roleName) =>
        gate.ApplyAsync(
            TENANT_ID,
            AuthProviders.Keycloak,
            "external-1",
            userId: USER_ID,
            mappedRoles: [SystemRole.PlatformAdmin.Name, roleName],
            mappedPermissions: [Actions.Access.Hub, Actions.Platform.ManageTenants],
            TestContext.Current.CancellationToken);

    private AppRole AddRole(AppIdentityDbContext db, string name, long tenantId, bool isSystemDefined, params string[] permissionNames)
    {
        var role = new AppRole
        {
            Id = _nextId++,
            Name = name,
            NormalizedName = name.ToUpperInvariant(),
            TenantId = tenantId,
            IsSystemDefined = isSystemDefined,
            IsActive = true
        };
        db.Roles.Add(role);
        foreach (var permissionName in permissionNames)
        {
            var permission = db.Permissions.Local.FirstOrDefault(item => item.Name == permissionName)
                ?? db.Permissions.Add(new Permission(permissionName) { Id = _nextId++ }).Entity;
            db.RolePermissions.Add(new RolePermission(role.Id, permission.Id) { Id = _nextId++ });
        }

        return role;
    }
}
