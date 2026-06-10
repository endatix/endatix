using Endatix.Infrastructure.Identity.Provisioning;

namespace Endatix.Infrastructure.Tests.Identity.Provisioning;

public sealed class ExternalOperatorProvisionerTests
{
    [Fact]
    public void BuildExternalUserName_WithKeycloakGuidSubject_ReturnsIdentityPolicySafeUserName()
    {
        const string subjectId = "0f6d8b28-e761-4033-8e84-2ddebcec49ce";

        var userName = ExternalOperatorProvisioner.BuildExternalUserName("Keycloak", subjectId);

        userName.Should().NotContain(":");
        userName.Should().NotContain("-");
        userName.All(char.IsLetterOrDigit).Should().BeTrue();
        userName.Should().StartWith("Keycloak0f6d8b28e76140338e842ddebcec49ce");
        userName.Length.Should().BeLessThanOrEqualTo(256);
    }
}
