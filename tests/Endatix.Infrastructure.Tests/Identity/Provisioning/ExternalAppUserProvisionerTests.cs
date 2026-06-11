using Endatix.Infrastructure.Identity;
using Endatix.Infrastructure.Identity.Provisioning;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;

namespace Endatix.Infrastructure.Tests.Identity.Provisioning;

public sealed class ExternalAppUserProvisionerTests
{
    [Fact]
    public void BuildExternalUserName_WithKeycloakGuidSubject_ReturnsIdentityPolicySafeUserName()
    {
        const string subjectId = "0f6d8b28-e761-4033-8e84-2ddebcec49ce";

        var userName = ExternalAppUserProvisioner.BuildExternalUserName("Keycloak", subjectId);

        userName.Should().NotContain(":");
        userName.Should().NotContain("-");
        userName.All(char.IsLetterOrDigit).Should().BeTrue();
        userName.Should().StartWith("Keycloak0f6d8b28e76140338e842ddebcec49ce");
        userName.Length.Should().BeLessThanOrEqualTo(256);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task ProvisionAsync_WithMissingEmail_ReturnsInvalidBeforePersistence(string? email)
    {
        var userManager = Substitute.For<UserManager<AppUser>>(
            Substitute.For<IUserStore<AppUser>>(),
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            null);
        ExternalAppUserProvisioner provisioner = new(
            identityDbContext: null!,
            userManager,
            Substitute.For<ILogger<ExternalAppUserProvisioner>>());

        var result = await provisioner.ProvisionAsync(
            tenantId: 1,
            authProvider: "Keycloak",
            externalSubjectId: "subject-123",
            mappedAppRoles: ["Admin"],
            identityProfile: new ExternalIdentityProfile(email, "AppUser"),
            cancellationToken: CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.ValidationErrors.Should().ContainSingle(error => error.ErrorMessage == "App user email is required.");
        await userManager.DidNotReceiveWithAnyArgs().CreateAsync(default!);
        await userManager.DidNotReceiveWithAnyArgs().UpdateAsync(default!);
    }

}
