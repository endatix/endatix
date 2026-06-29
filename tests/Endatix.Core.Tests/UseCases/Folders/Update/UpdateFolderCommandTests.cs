using Endatix.Core.UseCases.Folders.Update;

namespace Endatix.Core.Tests.UseCases.Folders.Update;

public class UpdateFolderCommandTests
{
    [Fact]
    public void Constructor_ValidFolderId_CreatesCommand()
    {
        var command = new UpdateFolderCommand(1L);

        command.FolderId.Should().Be(1L);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Constructor_InvalidFolderId_ThrowsArgumentException(long folderId)
    {
        Action act = () => _ = new UpdateFolderCommand(folderId);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Constructor_AllProperties_AreNullable()
    {
        var command = new UpdateFolderCommand(1L);

        command.Name.Should().BeNull();
        command.Slug.Should().BeNull();
        command.Description.Should().BeNull();
        command.Metadata.Should().BeNull();
        command.IsActive.Should().BeNull();
        command.Immutable.Should().BeNull();
    }

    [Fact]
    public void SetProperties_AssignsValues()
    {
        var command = new UpdateFolderCommand(1L)
        {
            Name = "Updated Name",
            Slug = "updated-slug",
            Description = "Updated description",
            Metadata = @"{""key"": ""value""}",
            IsActive = false,
            Immutable = true
        };

        command.Name.Should().Be("Updated Name");
        command.Slug.Should().Be("updated-slug");
        command.Description.Should().Be("Updated description");
        command.Metadata.Should().Be(@"{""key"": ""value""}");
        command.IsActive.Should().BeFalse();
        command.Immutable.Should().BeTrue();
    }
}