using Endatix.Core.Infrastructure.Paging;

namespace Endatix.Core.Tests.Infrastructure.Paging;

public sealed class UtcDateTimeRangeTests
{
    [Fact]
    public void Default_HasNoBounds()
    {
        UtcDateTimeRange range = default;

        range.HasBounds.Should().BeFalse();
        range.InclusiveFrom.Should().BeNull();
        range.ExclusiveTo.Should().BeNull();
    }

    [Fact]
    public void HasBounds_TrueWhenInclusiveFromSet()
    {
        var range = new UtcDateTimeRange(new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc), null);

        range.HasBounds.Should().BeTrue();
    }

    [Fact]
    public void HasBounds_TrueWhenExclusiveToSet()
    {
        var range = new UtcDateTimeRange(null, new DateTime(2024, 1, 2, 0, 0, 0, DateTimeKind.Utc));

        range.HasBounds.Should().BeTrue();
    }

    [Fact]
    public void HasBounds_TrueWhenBothSet()
    {
        var from = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var to = new DateTime(2024, 1, 2, 0, 0, 0, DateTimeKind.Utc);
        var range = new UtcDateTimeRange(from, to);

        range.HasBounds.Should().BeTrue();
        range.InclusiveFrom.Should().Be(from);
        range.ExclusiveTo.Should().Be(to);
    }
}
