using Endatix.Core.Abstractions.Authorization;
using Endatix.Core.Entities;
using Endatix.Core.Exceptions;
using Endatix.Core.Infrastructure.Result;

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
    [InlineData("Respondent")]
    [InlineData("Creator")]
    [InlineData("Admin")]
    [InlineData("Regional Reviewer")] // custom tenant role: existence is the write boundary's check
    public void ValidateDefaultRegistrationRole_AllowedRole_ReturnsSuccess(string roleName)
    {
        // Act
        var result = TenantSettings.ValidateDefaultRegistrationRole(roleName);

        // Assert
        result.IsSuccess.Should().BeTrue();
    }

    [Theory]
    [InlineData("PlatformAdmin", "platform-scoped")]
    [InlineData("Public", "anonymous role")]
    [InlineData("Authenticated", "not a persisted role")]
    public void ValidateDefaultRegistrationRole_ForbiddenRole_ExplainsWhy(string roleName, string expectedReason)
    {
        // Act
        var result = TenantSettings.ValidateDefaultRegistrationRole(roleName);

        // Assert
        result.Status.Should().Be(ResultStatus.Invalid);
        var error = result.ValidationErrors.Should().ContainSingle().Subject;
        error.Identifier.Should().Be(nameof(TenantSettings.DefaultRegistrationRoleName));
        // The caller must not have to re-derive the reason: it names the offending role and the rule.
        error.ErrorMessage.Should().Contain(roleName).And.Contain(expectedReason);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void ValidateDefaultRegistrationRole_BlankRole_ReturnsInvalidRatherThanThrowing(string? roleName)
    {
        // Act
        var result = TenantSettings.ValidateDefaultRegistrationRole(roleName);

        // Assert
        result.Status.Should().Be(ResultStatus.Invalid);
        result.ValidationErrors.Should().ContainSingle()
            .Which.Identifier.Should().Be(nameof(TenantSettings.DefaultRegistrationRoleName));
    }

    [Theory]
    [InlineData("PlatformAdmin")]
    [InlineData("Public")]
    [InlineData("Authenticated")]
    [InlineData("")]
    public void UpdateSelfRegistrationPolicy_ForbiddenDefaultRole_ThrowsEndUserSafeException(string roleName)
    {
        // Arrange
        var settings = new TenantSettings(tenantId: 1);

        // Act
        var act = () => settings.UpdateSelfRegistrationPolicy(
            allowSelfRegistration: true,
            allowedAuthProviderKeys: null,
            defaultRegistrationRoleName: roleName);

        // Assert
        var thrown = act.Should().Throw<DomainValidationException>().Which;
        thrown.Should().BeAssignableTo<ArgumentException>("existing catch (ArgumentException) sites must keep working");
        thrown.Should().BeAssignableTo<IEndUserSafeError>("otherwise the handler masks the reason and logs it as an error");

        // EndUserMessage is held separately so ArgumentException's " (Parameter 'x')" suffix never leaks.
        thrown.EndUserMessage.Should().NotContain("Parameter");
        thrown.EndUserMessage.Should().Be(
            TenantSettings.ValidateDefaultRegistrationRole(roleName).ValidationErrors.Single().ErrorMessage,
            "the throw and the Result path must not drift apart");
    }

    [Fact]
    public void UpdateSelfRegistrationPolicy_ForbiddenDefaultRole_LeavesSettingsUnchanged()
    {
        // Arrange
        var settings = new TenantSettings(tenantId: 1);

        // Act
        var act = () => settings.UpdateSelfRegistrationPolicy(
            allowSelfRegistration: true,
            allowedAuthProviderKeys: ["endatix"],
            defaultRegistrationRoleName: SystemRole.PlatformAdmin.Name);

        // Assert
        act.Should().Throw<DomainValidationException>();
        settings.AllowSelfRegistration.Should().BeFalse();
        settings.DefaultRegistrationRoleName.Should().Be(TenantSettings.DefaultRegistrationRole);
        settings.AllowedAuthProviderKeys.Should().BeEmpty();
    }
}
