using Endatix.Core.Infrastructure.Paging;

namespace Endatix.Core.Tests.Infrastructure.Paging;

public sealed class SortRequestTests
{
    private enum TestSortField
    {
        Name,
        CreatedAt,
    }

    [Fact]
    public void FromNullable_WhenBothMissing_ReturnsNull()
    {
        // Act
        var result = SortRequest<TestSortField>.FromNullable(
            null,
            null,
            TestSortField.Name);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public void FromNullableOrDefault_WhenBothMissing_UsesDefaultFieldAndDirection()
    {
        // Act
        var result = SortRequest<TestSortField>.FromNullableOrDefault(
            null,
            null,
            TestSortField.CreatedAt,
            SortDirection.Desc);

        // Assert
        result.Field.Should().Be(TestSortField.CreatedAt);
        result.Direction.Should().Be(SortDirection.Desc);
    }

    [Fact]
    public void FromNullable_WhenOnlyDirectionProvided_UsesDefaultField()
    {
        // Act
        var result = SortRequest<TestSortField>.FromNullable(
            null,
            SortDirection.Desc,
            TestSortField.Name);

        // Assert
        result.Should().NotBeNull();
        result!.Field.Should().Be(TestSortField.Name);
        result.Direction.Should().Be(SortDirection.Desc);
    }
}
