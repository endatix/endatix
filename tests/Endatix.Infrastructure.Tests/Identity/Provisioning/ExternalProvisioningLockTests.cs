using Endatix.Infrastructure.Identity.Provisioning;

namespace Endatix.Infrastructure.Tests.Identity.Provisioning;

public sealed class ExternalProvisioningLockTests
{
    [Fact]
    public void Get_WithSameKey_ReturnsSameSemaphoreInstance()
    {
        var first = ExternalProvisioningLock.Get(1, "Keycloak", "subject-a");
        var second = ExternalProvisioningLock.Get(1, "Keycloak", "subject-a");

        ReferenceEquals(first, second).Should().BeTrue();
    }

    [Fact]
    public void Get_WithDifferentKeys_ReturnsDifferentSemaphoreInstances()
    {
        var first = ExternalProvisioningLock.Get(1, "Keycloak", "subject-a");
        var second = ExternalProvisioningLock.Get(1, "Keycloak", "subject-b");

        ReferenceEquals(first, second).Should().BeFalse();
    }
}
