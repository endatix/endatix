using Endatix.Core.UseCases.TenantSettings.PartialUpdate;

namespace Endatix.Core.Tests.UseCases.TenantSettings.PartialUpdate;

public class PartialUpdateTenantSettingsCommandTests
{
    [Fact]
    public void Constructor_DefaultProperties_AreNull()
    {
        var command = new PartialUpdateTenantSettingsCommand();

        command.RequireFolderAssignment.Should().BeNull();
        command.SubmissionTokenExpiryHours.Should().BeNull();
        command.ClearSubmissionTokenExpiryHours.Should().BeFalse();
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Constructor_SetRequireFolderAssignment_ValueIsPreserved(bool value)
    {
        var command = new PartialUpdateTenantSettingsCommand { RequireFolderAssignment = value };

        command.RequireFolderAssignment.Should().Be(value);
    }

    [Fact]
    public void Constructor_SetSubmissionTokenExpiryHours_ValueIsPreserved()
    {
        var command = new PartialUpdateTenantSettingsCommand
        {
            SubmissionTokenExpiryHours = 168,
            ClearSubmissionTokenExpiryHours = false,
        };

        command.SubmissionTokenExpiryHours.Should().Be(168);
        command.ClearSubmissionTokenExpiryHours.Should().BeFalse();
    }
}