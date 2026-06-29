using Endatix.Core.UseCases.Folders.List;

namespace Endatix.Core.Tests.UseCases.Folders.List;

public class ListFoldersQueryTests
{
    [Fact]
    public void Constructor_DefaultIncludeInactive_IsFalse()
    {
        var query = new ListFoldersQuery();

        query.IncludeInactive.Should().BeFalse();
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Constructor_SetIncludeInactive_ValueIsPreserved(bool includeInactive)
    {
        var query = new ListFoldersQuery(includeInactive);

        query.IncludeInactive.Should().Be(includeInactive);
    }
}