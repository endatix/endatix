using Endatix.Infrastructure.Identity;
using Endatix.Infrastructure.Identity.Authorization.Data;

namespace Endatix.Infrastructure.Tests.Identity.Authorization;

public class DefaultAuthorizationDataProviderTenantScopeTests
{
    [Fact]
    public void IsRoleInAuthorizationScope_IncludesOnlyCurrentTenantAndGlobalSystemRoles()
    {
        // Arrange
        const long currentTenantId = 10;
        AppRole currentTenantRole = new()
        {
            TenantId = currentTenantId,
            IsSystemDefined = false
        };
        AppRole globalSystemRole = new()
        {
            TenantId = 0,
            IsSystemDefined = true
        };
        AppRole otherTenantRole = new()
        {
            TenantId = 20,
            IsSystemDefined = false
        };
        AppRole nonSystemGlobalRole = new()
        {
            TenantId = 0,
            IsSystemDefined = false
        };

        // Act
        var includedRoles = new[]
            {
                currentTenantRole,
                globalSystemRole,
                otherTenantRole,
                nonSystemGlobalRole
            }
            .AsQueryable()
            .Where(DefaultAuthorizationDataProvider.IsRoleInAuthorizationScope(currentTenantId))
            .ToList();

        // Assert
        includedRoles.Should().BeEquivalentTo([currentTenantRole, globalSystemRole]);
    }

    [Fact]
    public void IsUserInAuthorizationTenantScope_ReturnsTrue_WhenUserBelongsToRequestedTenant()
    {
        // Arrange
        AppUser user = new()
        {
            TenantId = 10
        };

        // Act
        var isInScope = DefaultAuthorizationDataProvider.IsUserInAuthorizationTenantScope(user, 10);

        // Assert
        isInScope.Should().BeTrue();
    }

    [Fact]
    public void IsUserInAuthorizationTenantScope_ReturnsFalse_WhenUserDoesNotBelongToRequestedTenant()
    {
        // Arrange
        AppUser user = new()
        {
            TenantId = 10
        };

        // Act
        var isInScope = DefaultAuthorizationDataProvider.IsUserInAuthorizationTenantScope(user, 20);

        // Assert
        isInScope.Should().BeFalse();
    }
}
