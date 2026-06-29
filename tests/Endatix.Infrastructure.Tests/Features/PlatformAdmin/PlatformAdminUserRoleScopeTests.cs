using Endatix.Infrastructure.Features.PlatformAdmin.Common;
using Endatix.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity;

namespace Endatix.Infrastructure.Tests.Features.PlatformAdmin;

public sealed class PlatformAdminUserRoleScopeTests
{
    private const long PlatformAdminRoleId = 100;

    [Fact]
    public void Apply_IgnoreScope_ReturnsAllUsers()
    {
        // Arrange
        var users = CreateUsers(1, 2, 3).AsQueryable();
        var userRoles = CreateUserRoles((1, PlatformAdminRoleId)).AsQueryable();

        // Act
        var result = PlatformAdminUserRoleScope.Apply(
                users,
                userRoles,
                PlatformAdminRoleId,
                PlatformAdminUserScopeFilter.IgnoreLocalPlatformAdminRole)
            .Select(user => user.Id)
            .ToList();

        // Assert
        result.Should().BeEquivalentTo([1, 2, 3]);
    }

    [Fact]
    public void Apply_MustHaveLocalPlatformAdminRole_ReturnsOnlyAssignedUsers()
    {
        // Arrange
        var users = CreateUsers(1, 2, 3).AsQueryable();
        var userRoles = CreateUserRoles(
            (1, PlatformAdminRoleId),
            (3, PlatformAdminRoleId)).AsQueryable();

        // Act
        var result = PlatformAdminUserRoleScope.Apply(
                users,
                userRoles,
                PlatformAdminRoleId,
                PlatformAdminUserScopeFilter.MustHaveLocalPlatformAdminRole)
            .Select(user => user.Id)
            .ToList();

        // Assert
        result.Should().BeEquivalentTo([1, 3]);
    }

    [Fact]
    public void Apply_MustNotHaveLocalPlatformAdminRole_ExcludesAssignedUsers()
    {
        // Arrange
        var users = CreateUsers(1, 2, 3).AsQueryable();
        var userRoles = CreateUserRoles(
            (1, PlatformAdminRoleId),
            (3, PlatformAdminRoleId)).AsQueryable();

        // Act
        var result = PlatformAdminUserRoleScope.Apply(
                users,
                userRoles,
                PlatformAdminRoleId,
                PlatformAdminUserScopeFilter.MustNotHaveLocalPlatformAdminRole)
            .Select(user => user.Id)
            .ToList();

        // Assert
        result.Should().BeEquivalentTo([2]);
    }

    [Fact]
    public void Apply_WhenRoleIdMissing_DoesNotFilterUsers()
    {
        // Arrange
        var users = CreateUsers(1, 2).AsQueryable();
        var userRoles = CreateUserRoles((1, PlatformAdminRoleId)).AsQueryable();

        // Act
        var result = PlatformAdminUserRoleScope.Apply(
                users,
                userRoles,
                platformAdminRoleId: null,
                PlatformAdminUserScopeFilter.MustHaveLocalPlatformAdminRole)
            .Select(user => user.Id)
            .ToList();

        // Assert
        result.Should().BeEquivalentTo([1, 2]);
    }

    private static IEnumerable<AppUser> CreateUsers(params long[] userIds) =>
        userIds.Select(userId => new AppUser
        {
            Id = userId,
            TenantId = 1,
            UserName = $"user-{userId}",
        });

    private static IEnumerable<IdentityUserRole<long>> CreateUserRoles(
        params (long UserId, long RoleId)[] assignments) =>
        assignments.Select(assignment => new IdentityUserRole<long>
        {
            UserId = assignment.UserId,
            RoleId = assignment.RoleId,
        });
}
