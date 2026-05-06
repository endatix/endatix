using Endatix.Core.UseCases.Folders.Delete;

namespace Endatix.Core.Tests.UseCases.Folders.Delete;

public class DeleteFolderCommandTests
{
    [Fact]
    public void Constructor_ValidFolderId_CreatesCommand()
    {
        var command = new DeleteFolderCommand(1L);

        command.FolderId.Should().Be(1L);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Constructor_InvalidFolderId_ThrowsArgumentException(long folderId)
    {
        Action act = () => _ = new DeleteFolderCommand(folderId);

        act.Should().Throw<ArgumentException>();
    }
}