using Endatix.Modules.Audience.Domain;

namespace Endatix.Modules.Audience.Tests.Domain;

public class AudienceLinkTests
{
    [Fact]
    public void AccessStamp_NamesTheLink_AndChangesWithItsToken()
    {
        // Arrange
        var (link, _) = AudienceLink.Issue(1, 2, 3);
        link.Id = 0x1a2b3c;
        var (reissued, _) = AudienceLink.Issue(1, 2, 3);
        reissued.Id = link.Id;

        // Act
        var stamp = link.AccessStamp();

        // Assert
        Assert.Matches("^[0-9a-f]+$", stamp);
        Assert.True(AudienceLink.TryReadAccessStamp(stamp, out var linkId));
        Assert.Equal(link.Id, linkId);
        Assert.NotEqual(stamp, reissued.AccessStamp());
    }

    [Theory]
    [InlineData("0a1b2c3d")]
    [InlineData("zz0123456789abcdef")]
    public void TryReadAccessStamp_AnyOtherValue_NamesNoLink(string stamp)
    {
        // Act
        var read = AudienceLink.TryReadAccessStamp(stamp, out _);

        // Assert
        Assert.False(read);
    }
}
