using Endatix.Core.Exceptions;
using Endatix.Modules.Reporting.Domain;

namespace Endatix.Modules.Reporting.Tests.Domain;

public sealed class SubmissionViewTests
{
    private const long TenantId = 1;
    private const long FormId = 100;
    private const long UserId = 7;
    private const string Settings = """{"v":1,"columns":[{"key":"q1","visible":false}]}""";

    [Fact]
    public void CreateShared_ZeroTenant_ThrowsArgumentException()
    {
        // Arrange
        const long tenantId = 0;

        // Act
        var act = () => SubmissionView.CreateShared(new SubmissionViewCreateArgs(tenantId, FormId, Settings, UserId));

        // Assert
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void CreateShared_MalformedSettings_ThrowsDomainValidationException()
    {
        // Arrange
        const string settings = """{"v":2,"columns":[]}""";

        // Act
        var act = () => SubmissionView.CreateShared(new SubmissionViewCreateArgs(TenantId, FormId, settings, UserId));

        // Assert
        act.Should().Throw<DomainValidationException>()
            .Which.EndUserMessage.Should().Be(SubmissionViewSettings.Error(settings));
    }

    [Fact]
    public void CreateShared_ValidSettings_StartsAtFirstVersionOwnedByEveryone()
    {
        // Act
        var view = SubmissionView.CreateShared(new SubmissionViewCreateArgs(TenantId, FormId, Settings, UserId));

        // Assert
        view.TenantId.Should().Be(TenantId);
        view.FormId.Should().Be(FormId);
        view.OwnerUserId.Should().Be(SubmissionView.SharedOwnerUserId);
        view.SettingsJson.Should().Be(Settings);
        view.Version.Should().Be(SubmissionView.UnsavedVersion + 1);
        view.ModifiedBy.Should().Be(UserId);
    }

    [Fact]
    public void UpdateSettings_ValidSettings_ReplacesSettingsAndMovesToNextVersion()
    {
        // Arrange
        var view = SubmissionView.CreateShared(new SubmissionViewCreateArgs(TenantId, FormId, Settings, UserId));
        const string newSettings = """{"v":1,"columns":[],"pageSize":50}""";
        const long otherUserId = 8;

        // Act
        view.UpdateSettings(newSettings, otherUserId);

        // Assert
        view.SettingsJson.Should().Be(newSettings);
        view.Version.Should().Be(2);
        view.ModifiedBy.Should().Be(otherUserId);
    }

    [Fact]
    public void UpdateSettings_MalformedSettings_KeepsSettingsAndVersion()
    {
        // Arrange
        var view = SubmissionView.CreateShared(new SubmissionViewCreateArgs(TenantId, FormId, Settings, UserId));
        const string malformed = "not json";

        // Act
        var act = () => view.UpdateSettings(malformed, UserId);

        // Assert
        act.Should().Throw<DomainValidationException>();
        view.SettingsJson.Should().Be(Settings);
        view.Version.Should().Be(1);
    }
}
