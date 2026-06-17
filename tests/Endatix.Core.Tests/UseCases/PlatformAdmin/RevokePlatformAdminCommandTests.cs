using Endatix.Core.UseCases.PlatformAdmin.RevokePlatformAdmin;

namespace Endatix.Core.Tests.UseCases.PlatformAdmin;

public class RevokePlatformAdminCommandTests
{
    [Fact]
    public void Constructor_ValidUserId_CreatesCommand()
    {
        var command = new RevokePlatformAdminCommand(1L);

        command.UserId.Should().Be(1L);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Constructor_InvalidUserId_ThrowsArgumentException(long userId)
    {
        Action act = () => _ = new RevokePlatformAdminCommand(userId);

        act.Should().Throw<ArgumentException>();
    }
}
