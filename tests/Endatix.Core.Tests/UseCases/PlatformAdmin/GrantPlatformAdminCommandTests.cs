using Endatix.Core.UseCases.PlatformAdmin.GrantPlatformAdmin;

namespace Endatix.Core.Tests.UseCases.PlatformAdmin;

public class GrantPlatformAdminCommandTests
{
    [Fact]
    public void Constructor_ValidUserId_CreatesCommand()
    {
        var command = new GrantPlatformAdminCommand(1L);

        command.UserId.Should().Be(1L);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Constructor_InvalidUserId_ThrowsArgumentException(long userId)
    {
        Action act = () => _ = new GrantPlatformAdminCommand(userId);

        act.Should().Throw<ArgumentException>();
    }
}
