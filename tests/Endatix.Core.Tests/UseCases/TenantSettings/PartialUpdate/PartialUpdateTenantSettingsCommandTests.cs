using Endatix.Core.UseCases.TenantSettings.PartialUpdate;

namespace Endatix.Core.Tests.UseCases.TenantSettings.PartialUpdate;

public class PartialUpdateTenantSettingsCommandTests
{
    [Fact]
    public void Constructor_DefaultProperties_AreNull()
    {
        var command = new PartialUpdateTenantSettingsCommand();

        command.RequireFolderAssignment.Should().BeNull();
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Constructor_SetRequireFolderAssignment_ValueIsPreserved(bool value)
    {
        var command = new PartialUpdateTenantSettingsCommand { RequireFolderAssignment = value };

        command.RequireFolderAssignment.Should().Be(value);
    }
}