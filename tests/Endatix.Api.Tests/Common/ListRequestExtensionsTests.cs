using Endatix.Api.Common;

namespace Endatix.Api.Tests.Common;

public sealed class ListRequestExtensionsTests
{
    private sealed class CreatedRangeStub : ICreatedRange
    {
        public string? CreatedFrom { get; set; }
        public string? CreatedTo { get; set; }
    }

    [Fact]
    public void ToCreatedRange_ParsesInclusiveFromAndExclusiveTo()
    {
        CreatedRangeStub request = new()
        {
            CreatedFrom = "2024-01-15",
            CreatedTo = "2024-01-31",
        };

        var range = request.ToCreatedRange();

        range.InclusiveFrom.Should().Be(new DateTime(2024, 1, 15, 0, 0, 0, DateTimeKind.Utc));
        range.ExclusiveTo.Should().Be(new DateTime(2024, 2, 1, 0, 0, 0, DateTimeKind.Utc));
        range.HasBounds.Should().BeTrue();
    }

    [Fact]
    public void ToCreatedRange_EmptyWhenBothOmitted()
    {
        CreatedRangeStub request = new();

        var range = request.ToCreatedRange();

        range.HasBounds.Should().BeFalse();
    }

    [Fact]
    public void ToCreatedRange_ClampsDateOnlyMaxValue()
    {
        CreatedRangeStub request = new()
        {
            CreatedTo = "9999-12-31",
        };

        var range = request.ToCreatedRange();

        range.ExclusiveTo.Should().Be(DateTime.SpecifyKind(DateTime.MaxValue, DateTimeKind.Utc));
    }
}
