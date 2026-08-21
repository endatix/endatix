using Endatix.Infrastructure.Identity;
using Endatix.Infrastructure.Identity.Authorization.Data;

namespace Endatix.Infrastructure.Tests.Identity.Authorization.Data;

public class DefaultAuthorizationDataProviderTests
{
    [Fact]
    public void IsUserInAuthorizationTenantScope_MatchingTenant_ReturnsTrue()
    {
        var user = new AppUser { Id = 7, TenantId = 10 };

        DefaultAuthorizationDataProvider.IsUserInAuthorizationTenantScope(user, 10).Should().BeTrue();
    }

    [Fact]
    public void IsUserInAuthorizationTenantScope_MismatchedTenant_ReturnsFalse()
    {
        var user = new AppUser { Id = 7, TenantId = 10 };

        DefaultAuthorizationDataProvider.IsUserInAuthorizationTenantScope(user, 99).Should().BeFalse();
    }

    [Fact]
    public void IsAssumeTenantSession_ActorMatchesDifferentTenant_ReturnsTrue()
    {
        DefaultAuthorizationDataProvider.IsAssumeTenantSession(7, 7, homeTenantId: 1, requestedTenantId: 99)
            .Should().BeTrue();
    }

    [Fact]
    public void IsAssumeTenantSession_NoActor_ReturnsFalse()
    {
        DefaultAuthorizationDataProvider.IsAssumeTenantSession(7, null, homeTenantId: 1, requestedTenantId: 99)
            .Should().BeFalse();
    }

    [Fact]
    public void IsAssumeTenantSession_HomeTenant_ReturnsFalse()
    {
        DefaultAuthorizationDataProvider.IsAssumeTenantSession(7, 7, homeTenantId: 10, requestedTenantId: 10)
            .Should().BeFalse();
    }
}
