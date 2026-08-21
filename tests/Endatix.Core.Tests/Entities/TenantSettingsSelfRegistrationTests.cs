using Endatix.Core.Abstractions.Authorization;
using Endatix.Core.Entities;

namespace Endatix.Core.Tests.Entities;

public class TenantSettingsSelfRegistrationTests
{
    [Fact]
    public void Constructor_Defaults_SelfRegistrationDisabledWithRespondentRole()
    {
        // Arrange & Act
        var settings = new TenantSettings(tenantId: 1);

        // Assert
        settings.AllowSelfRegistration.Should().BeFalse();
        settings.DefaultRegistrationRoleName.Should().Be(TenantSettings.DefaultRegistrationRole);
        settings.AllowedAuthProviderKeys.Should().BeEmpty();
    }

    [Fact]
    public void UpdateSelfRegistrationPolicy_ValidPolicy_UpdatesFields()
    {
        // Arrange
        var settings = new TenantSettings(tenantId: 1);

        // Act
        settings.UpdateSelfRegistrationPolicy(
            allowSelfRegistration: true,
            allowedAuthProviderKeys: ["endatix", "google"],
            defaultRegistrationRoleName: SystemRole.Respondent.Name);

        // Assert
        settings.AllowSelfRegistration.Should().BeTrue();
        settings.DefaultRegistrationRoleName.Should().Be(SystemRole.Respondent.Name);
        settings.AllowedAuthProviderKeys.Should().BeEquivalentTo(["endatix", "google"]);
    }

    [Theory]
    [InlineData("PlatformAdmin")]
    [InlineData("Public")]
    [InlineData("Authenticated")]
    public void UpdateSelfRegistrationPolicy_ForbiddenDefaultRole_ThrowsArgumentException(string roleName)
    {
        // Arrange
        var settings = new TenantSettings(tenantId: 1);

        // Act
        var act = () => settings.UpdateSelfRegistrationPolicy(
            allowSelfRegistration: true,
            allowedAuthProviderKeys: null,
            defaultRegistrationRoleName: roleName);

        // Assert
        act.Should().Throw<ArgumentException>();
        TenantSettings.IsAllowedDefaultRegistrationRole(roleName).Should().BeFalse();
    }

    [Fact]
    public void IsAllowedDefaultRegistrationRole_Respondent_ReturnsTrue()
    {
        // Arrange & Act & Assert
        TenantSettings.IsAllowedDefaultRegistrationRole(SystemRole.Respondent.Name).Should().BeTrue();
        TenantSettings.IsAllowedDefaultRegistrationRole(SystemRole.Creator.Name).Should().BeTrue();
    }
}
