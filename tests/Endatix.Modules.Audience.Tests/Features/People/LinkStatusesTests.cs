using Endatix.Modules.Audience.Features.People;

namespace Endatix.Modules.Audience.Tests.Features.People;

public sealed class LinkStatusesTests
{
    private static readonly DateTime OpenedAt = new(2026, 10, 21, 9, 30, 0, DateTimeKind.Utc);

    [Fact]
    public void Of_NoLink_IsNone()
    {
        LinkStatuses.Of(null, null).Should().Be(new LinkStatusDto(LinkStatusKind.None));
    }

    [Fact]
    public void Of_UnopenedLink_IsIssued()
    {
        LinkStatuses.Of(new LinkClaim(null, null), null).Should().Be(new LinkStatusDto(LinkStatusKind.Issued));
    }

    [Fact]
    public void Of_LinkThatOpenedALiveSubmission_IsOpenedWithIt()
    {
        var status = LinkStatuses.Of(new LinkClaim(OpenedAt, 7), new ClaimedSubmission(7, IsDeleted: false, IsTest: false));

        status.Should().Be(new LinkStatusDto(LinkStatusKind.Opened, OpenedAt, 7));
    }

    [Fact]
    public void Of_LinkWhoseSubmissionWasDeleted_IsOpenedWithoutIt()
    {
        var status = LinkStatuses.Of(new LinkClaim(OpenedAt, 7), new ClaimedSubmission(7, IsDeleted: true, IsTest: false));

        status.Should().Be(new LinkStatusDto(LinkStatusKind.Opened, OpenedAt));
    }

    [Fact]
    public void Of_LinkWhoseSubmissionIsGone_IsOpenedWithoutIt()
    {
        LinkStatuses.Of(new LinkClaim(OpenedAt, 7), null).Should().Be(new LinkStatusDto(LinkStatusKind.Opened, OpenedAt));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Of_LinkWhoseSubmissionWasMarkedAsTest_IsIssued(bool isDeleted)
    {
        var status = LinkStatuses.Of(new LinkClaim(OpenedAt, 7), new ClaimedSubmission(7, isDeleted, IsTest: true));

        status.Should().Be(new LinkStatusDto(LinkStatusKind.Issued));
    }
}
